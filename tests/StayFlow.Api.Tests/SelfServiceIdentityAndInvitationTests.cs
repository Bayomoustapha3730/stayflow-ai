using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.DTOs.Organizations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Email;

namespace StayFlow.Api.Tests;

public sealed class SelfServiceIdentityAndInvitationTests
{
    [Fact]
    public async Task RegisterAsync_WithoutInvitation_CreatesOrganizationBootstrapAndSession()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"organization-registration-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new ApplicationDbContext(options);
        var configuration = CreateConfiguration();
        var hasher = new Pbkdf2PasswordHasher();
        var subscriptions = new RecordingSubscriptionEntitlementService(dbContext);
        var service = new AuthService(new AuthRepository(dbContext), new JwtTokenService(configuration, hasher), hasher, configuration,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, new NoOpIdentityEmailService(), dbContext, subscriptions,
            new TenantExecutionContextAccessor());

        var response = await service.RegisterAsync(new RegisterRequest
        {
            FullName = "Organization Owner",
            Email = "owner@example.com",
            PhoneNumber = "+254700000003",
            Password = "StrongPassword1!",
            OrganizationName = "Owner Organization",
            OrganizationSlug = "owner-organization",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi"
        }, CancellationToken.None);

        Assert.True(response.Success, response.Message);
        var user = await dbContext.Users.SingleAsync(item => item.NormalizedEmail == "OWNER@EXAMPLE.COM");
        Assert.Single(await dbContext.Companies.Where(item => item.Id == user.CompanyId).ToListAsync());
        Assert.Single(await dbContext.OrganizationMembers.Where(item => item.UserId == user.Id && item.CompanyId == user.CompanyId && item.Role == OrganizationRole.Owner.ToStorageValue()).ToListAsync());
        Assert.Single(await dbContext.OnboardingProgressRecords.Where(item => item.UserId == user.Id && item.CompanyId == user.CompanyId).ToListAsync());
        Assert.Single(await dbContext.TenantSubscriptions.Where(item => item.CompanyId == user.CompanyId).ToListAsync());
        Assert.Single(await dbContext.RefreshTokens.Where(item => item.UserId == user.Id).ToListAsync());
    }

    [Fact]
    public async Task RegisterAsync_WithValidInvitation_CreatesOnlyInvitedUserMembershipAndSession()
    {
        var fixture = await CreateInvitationRegistrationFixtureAsync(OnboardingStep.Completed.ToStorageValue());

        var response = await fixture.Service.RegisterAsync(NewInvitationRegistrationRequest(fixture.Token, " invited@example.com "), CancellationToken.None);

        Assert.True(response.Success, response.Message);
        Assert.NotNull(response.Data);
        Assert.Equal(fixture.InitialCompanyCount, await fixture.DbContext.Companies.CountAsync());
        var user = await fixture.DbContext.Users.SingleAsync(item => item.NormalizedEmail == "INVITED@EXAMPLE.COM");
        Assert.Equal(fixture.CompanyId, user.CompanyId);
        Assert.Equal(OrganizationRole.Host.ToStorageValue(), user.Role);
        Assert.Single(await fixture.DbContext.OrganizationMembers.Where(item => item.UserId == user.Id && item.CompanyId == fixture.CompanyId && item.Status == OrganizationMemberStatus.Active.ToStorageValue()).ToListAsync());
        Assert.Empty(await fixture.DbContext.OnboardingProgressRecords.Where(item => item.UserId == user.Id).ToListAsync());
        Assert.Empty(await fixture.DbContext.TenantSubscriptions.Where(item => item.CompanyId == fixture.CompanyId && item.CreatedAt > fixture.StartedAt).ToListAsync());
        Assert.NotNull((await fixture.DbContext.OrganizationInvitations.SingleAsync(item => item.Id == fixture.InvitationId)).AcceptedAtUtc);
        Assert.Single(await fixture.DbContext.RefreshTokens.Where(item => item.UserId == user.Id).ToListAsync());
        var issuedToken = new JwtSecurityTokenHandler().ReadJwtToken(response.Data.AccessToken);
        Assert.Equal(fixture.CompanyId.ToString(), issuedToken.Claims.Single(claim => claim.Type == "company_id").Value);
    }

    [Theory]
    [InlineData("other@example.com", false, false)]
    [InlineData("invited@example.com", true, false)]
    [InlineData("invited@example.com", false, true)]
    public async Task RegisterAsync_WithInvalidInvitation_DoesNotCreatePartialAccount(string email, bool expired, bool revoked)
    {
        var fixture = await CreateInvitationRegistrationFixtureAsync(OnboardingStep.Completed.ToStorageValue(), expired, revoked);

        var response = await fixture.Service.RegisterAsync(NewInvitationRegistrationRequest(fixture.Token, email), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(fixture.InitialCompanyCount, await fixture.DbContext.Companies.CountAsync());
        Assert.Empty(await fixture.DbContext.Users.Where(item => item.NormalizedEmail == email.Trim(' ').ToUpperInvariant()).ToListAsync());
        Assert.Empty(await fixture.DbContext.OrganizationMembers.Where(item => item.CompanyId == fixture.CompanyId && item.User.Email == email).ToListAsync());
        Assert.Empty(await fixture.DbContext.RefreshTokens.ToListAsync());
        var invitation = await fixture.DbContext.OrganizationInvitations.SingleAsync(item => item.Id == fixture.InvitationId);
        Assert.Null(invitation.AcceptedAtUtc);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingGlobalUser_RequiresAuthenticatedAcceptance()
    {
        var fixture = await CreateInvitationRegistrationFixtureAsync(OnboardingStep.Completed.ToStorageValue());
        fixture.DbContext.Users.Add(NewUser(fixture.CompanyId, "invited@example.com"));
        await fixture.DbContext.SaveChangesAsync();

        var response = await fixture.Service.RegisterAsync(NewInvitationRegistrationRequest(fixture.Token, "invited@example.com"), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("Sign in to accept", response.Message, StringComparison.Ordinal);
        Assert.Equal(1, await fixture.DbContext.Users.CountAsync(item => item.NormalizedEmail == "INVITED@EXAMPLE.COM"));
        Assert.Null((await fixture.DbContext.OrganizationInvitations.SingleAsync(item => item.Id == fixture.InvitationId)).AcceptedAtUtc);
    }

    [Fact]
    public async Task RelationalGlobalIdentity_AllowsOneCanonicalEmailAndRejectsCompetingUser()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var schemaContext = new ApplicationDbContext(options))
        {
            await schemaContext.Database.EnsureCreatedAsync();
        }

        await using var firstContext = new ApplicationDbContext(options);
        await using var secondContext = new ApplicationDbContext(options);
        firstContext.Companies.Add(NewCompany(companyA, "identity-a"));
        secondContext.Companies.Add(NewCompany(companyB, "identity-b"));
        await firstContext.SaveChangesAsync();
        await secondContext.SaveChangesAsync();

        var firstUser = NewUser(companyA, "Person@Example.com");
        var secondUser = NewUser(companyB, " person@example.com ");
        firstContext.Users.Add(firstUser);
        secondContext.Users.Add(secondUser);

        await firstContext.SaveChangesAsync();
        var competingSave = () => secondContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateException>(competingSave);

        await using var verificationContext = new ApplicationDbContext(options);
        Assert.Equal(1, await verificationContext.Users.CountAsync(user => user.NormalizedEmail == "PERSON@EXAMPLE.COM"));
        Assert.Equal("PERSON@EXAMPLE.COM", firstUser.NormalizedEmail);

        firstUser.Email = " person@example.com ";
        await firstContext.SaveChangesAsync();
        Assert.Equal("PERSON@EXAMPLE.COM", firstUser.NormalizedEmail);
    }

    [Fact]
    public async Task InvitationAccept_MovesExistingUserAndRotatesRefreshSession()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var inviterUserId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        const string invitationToken = "invitation-token-for-test";
        const string oldRefreshToken = "old-company-a-refresh-token";
        var hasher = new Pbkdf2PasswordHasher();
        var configuration = CreateConfiguration();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"invitation-session-{Guid.NewGuid():N}")
            .Options;
        var user = NewUser(companyA, "member@example.com", userId);
        var inviter = NewUser(companyB, "inviter@example.com", inviterUserId);
        user.Email = "member@example.com";
        user.NormalizedEmail = "MEMBER@EXAMPLE.COM";
        user.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hasher.HashToken(oldRefreshToken),
            SessionId = Guid.NewGuid(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        });

        await using (var seedContext = new ApplicationDbContext(options))
        {
            seedContext.Companies.AddRange(NewCompany(companyA, "company-a"), NewCompany(companyB, "company-b"));
            seedContext.Users.AddRange(user, inviter);
            seedContext.OrganizationInvitations.Add(new OrganizationInvitation
            {
                Id = invitationId,
                CompanyId = companyB,
                InvitedByUserId = inviterUserId,
                Email = "member@example.com",
                NormalizedEmail = "MEMBER@EXAMPLE.COM",
                Role = OrganizationRole.Host.ToStorageValue(),
                TokenHash = hasher.HashToken($"{configuration["Jwt:SigningKey"]}:{invitationToken}"),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1)
            });
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = new ApplicationDbContext(options);
        var tenantContext = new TestTenantContext(companyA, userId);
        var invitationService = new OrganizationInvitationService(
            dbContext,
            tenantContext,
            hasher,
            configuration,
            new NoOpIdentityEmailService(),
            new JwtTokenService(configuration, hasher),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new TenantExecutionContextAccessor());

        var accepted = await invitationService.AcceptAsync(
            new AcceptOrganizationInvitationRequest { Token = invitationToken },
            CancellationToken.None);

        Assert.True(accepted.Success);
        Assert.NotNull(accepted.Data);
        Assert.Equal(companyB, (await dbContext.Users.SingleAsync(user => user.Id == userId)).CompanyId);
        Assert.NotNull(await dbContext.OrganizationMembers.SingleOrDefaultAsync(member => member.CompanyId == companyB && member.UserId == userId && member.Status == OrganizationMemberStatus.Active.ToStorageValue()));
        Assert.True(await dbContext.RefreshTokens.Where(token => token.UserId == userId).AllAsync(token => token.RevokedAt != null || token.TokenHash == hasher.HashToken(accepted.Data!.RefreshToken)));
        Assert.True((await dbContext.OrganizationInvitations.SingleAsync(invitation => invitation.Id == invitationId)).AcceptedAtUtc.HasValue);

        var authService = new AuthService(
            new AuthRepository(dbContext),
            new JwtTokenService(configuration, hasher),
            hasher,
            configuration,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new NoOpIdentityEmailService(),
            dbContext,
            NoOpSubscriptionEntitlementService.Instance,
            new TenantExecutionContextAccessor());

        var oldRefresh = await authService.RefreshAsync(new RefreshTokenRequest { RefreshToken = oldRefreshToken }, CancellationToken.None);
        Assert.False(oldRefresh.Success);

        var newRefresh = await authService.RefreshAsync(new RefreshTokenRequest { RefreshToken = accepted.Data.RefreshToken }, CancellationToken.None);
        Assert.True(newRefresh.Success);
        Assert.NotNull(newRefresh.Data);
        var refreshedToken = new JwtSecurityTokenHandler().ReadJwtToken(newRefresh.Data!.AccessToken);
        Assert.Equal(companyB.ToString(), refreshedToken.Claims.Single(claim => claim.Type == "company_id").Value);
    }

    private static User NewUser(Guid companyId, string email, Guid? id = null)
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            CompanyId = companyId,
            FullName = "Test Member",
            Email = email,
            PhoneNumber = "+254700000001",
            Role = OrganizationRole.Owner.ToStorageValue(),
            PasswordHash = "hash",
            IsActive = true
        };
    }

    private static Company NewCompany(Guid id, string slug)
    {
        return new Company
        {
            Id = id,
            Name = slug,
            Slug = slug,
            NormalizedSlug = slug.ToUpperInvariant(),
            Status = "Active",
            Email = $"{slug}@example.com",
            PhoneNumber = "+254700000002",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        };
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "StayFlow.Api.Tests",
                ["Jwt:Audience"] = "StayFlow.Tests",
                ["Jwt:SigningKey"] = "test-secret-key-with-at-least-32-characters",
                ["Jwt:RefreshTokenDays"] = "30",
                ["Email:Provider"] = "Development"
            })
            .Build();
    }

    private static RegisterRequest NewInvitationRegistrationRequest(string token, string email)
    {
        return new RegisterRequest
        {
            FullName = "Invited User",
            Email = email,
            PhoneNumber = "+254700000003",
            Password = "StrongPassword1!",
            InvitationToken = token,
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi"
        };
    }

    private static async Task<InvitationRegistrationFixture> CreateInvitationRegistrationFixtureAsync(string onboardingState, bool expired = false, bool revoked = false)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"invitation-registration-{Guid.NewGuid():N}")
            .Options;
        var dbContext = new ApplicationDbContext(options);
        var configuration = CreateConfiguration();
        var hasher = new Pbkdf2PasswordHasher();
        var companyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        const string token = "new-invitee-registration-token";
        var startedAt = DateTimeOffset.UtcNow;
        var company = NewCompany(companyId, "invited-company");
        company.OnboardingState = onboardingState;
        dbContext.Companies.Add(company);
        dbContext.Users.Add(NewUser(companyId, "inviter@example.com", inviterId));
        dbContext.OrganizationInvitations.Add(new OrganizationInvitation
        {
            Id = invitationId,
            CompanyId = companyId,
            InvitedByUserId = inviterId,
            Email = "invited@example.com",
            NormalizedEmail = "INVITED@EXAMPLE.COM",
            Role = OrganizationRole.Host.ToStorageValue(),
            TokenHash = hasher.HashToken($"{configuration["Jwt:SigningKey"]}:{token}"),
            ExpiresAtUtc = expired ? startedAt.AddMinutes(-1) : startedAt.AddDays(1),
            RevokedAtUtc = revoked ? startedAt : null
        });
        await dbContext.SaveChangesAsync();

        return new InvitationRegistrationFixture(
            dbContext,
            new AuthService(new AuthRepository(dbContext), new JwtTokenService(configuration, hasher), hasher, configuration,
                new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, new NoOpIdentityEmailService(),
                dbContext, NoOpSubscriptionEntitlementService.Instance, new TenantExecutionContextAccessor()),
            companyId, invitationId, token, startedAt, await dbContext.Companies.CountAsync());
    }

    private sealed record InvitationRegistrationFixture(ApplicationDbContext DbContext, AuthService Service, Guid CompanyId, Guid InvitationId, string Token, DateTimeOffset StartedAt, int InitialCompanyCount);

    private sealed class TestTenantContext(Guid companyId, Guid userId) : ICurrentTenantContext, ITenantContext
    {
        public Guid? TenantId => companyId;
        public Guid? CompanyId => companyId;
        public Guid? UserId => userId;
        public string? CorrelationId => "test-correlation";
        public bool IsAuthenticated => true;
    }

    private sealed class NoOpIdentityEmailService : IIdentityEmailService
    {
        public Task SendPasswordResetAsync(string email, string fullName, string token, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendEmailVerificationAsync(string email, string fullName, string token, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendOrganizationInvitationAsync(string email, string role, string token, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoOpSubscriptionEntitlementService : ISubscriptionEntitlementService
    {
        public static readonly NoOpSubscriptionEntitlementService Instance = new();
        public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(new SubscriptionSnapshot(companyId, Guid.NewGuid(), Guid.NewGuid(), "Free", "Free", "Active", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), [], []));
        public Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult<SubscriptionSnapshot?>(null);
        public Task EnsureFeatureEnabledAsync(Guid companyId, string featureKey, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<UsageConsumptionResult> ConsumeQuotaAsync(Guid companyId, UsageMetric metric, long quantity, string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(new UsageConsumptionResult(metric, null, 0, quantity, true, false));
        public Task<SubscriptionSnapshot> UpdatePlanAsync(Guid companyId, Guid? planId, string? planName, string? notes, CancellationToken cancellationToken) => GetCurrentSnapshotAsync(companyId, cancellationToken);
    }

    private sealed class RecordingSubscriptionEntitlementService(ApplicationDbContext dbContext) : ISubscriptionEntitlementService
    {
        public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken)
        {
            if (!dbContext.TenantSubscriptions.Local.Any(item => item.CompanyId == companyId))
            {
                dbContext.TenantSubscriptions.Add(new TenantSubscription
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    SubscriptionPlanId = SeedData.FreePlanId,
                    CurrentPeriodStartUtc = DateTimeOffset.UtcNow,
                    CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(30)
                });
            }

            return Task.FromResult(new SubscriptionSnapshot(companyId, Guid.NewGuid(), SeedData.FreePlanId, "Free", "Free", "Active", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), [], []));
        }

        public Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult<SubscriptionSnapshot?>(null);
        public Task EnsureFeatureEnabledAsync(Guid companyId, string featureKey, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<UsageConsumptionResult> ConsumeQuotaAsync(Guid companyId, UsageMetric metric, long quantity, string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(new UsageConsumptionResult(metric, null, 0, quantity, true, false));
        public Task<SubscriptionSnapshot> UpdatePlanAsync(Guid companyId, Guid? planId, string? planName, string? notes, CancellationToken cancellationToken) => GetCurrentSnapshotAsync(companyId, cancellationToken);
    }
}
