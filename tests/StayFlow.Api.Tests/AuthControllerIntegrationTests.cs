using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Tests;

public sealed class AuthControllerIntegrationTests : IClassFixture<SignalRTestAppFactory>
{
    private static readonly Guid DemoCompanyId = SeedData.DemoCompanyId;
    private static readonly Guid AccessibleCompanyId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid UnauthorizedCompanyId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid InactiveMembershipCompanyId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid DemoUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly SignalRTestAppFactory factory;

    public AuthControllerIntegrationTests(SignalRTestAppFactory factory)
    {
        this.factory = factory;
        this.factory.EnsureSeeded();
        EnsureUserExists();
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUserProfile()
    {
        EnsureOrganizationAccessFixture();

        using var client = CreateClientWithToken(DemoCompanyId, DemoUserId, ["auth.me"]);

        using var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("demo.user@stayflow.local", payload.RootElement.GetProperty("data").GetProperty("email").GetString());
        Assert.True(payload.RootElement.GetProperty("data").TryGetProperty("preferredLanguage", out _));
        Assert.True(payload.RootElement.GetProperty("data").TryGetProperty("timeZone", out _));
        Assert.Equal("Owner", payload.RootElement.GetProperty("data").GetProperty("organizationRole").GetString());
    }

    [Fact]
    public async Task Me_WithoutOrganizationMembership_DoesNotInventRole()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = dbContext.OrganizationMembers.FirstOrDefault(item => item.CompanyId == DemoCompanyId && item.UserId == DemoUserId);
        if (membership is not null)
        {
            dbContext.OrganizationMembers.Remove(membership);
            dbContext.SaveChanges();
        }

        using var client = CreateClientWithToken(DemoCompanyId, DemoUserId, ["auth.me"]);

        using var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("data").GetProperty("organizationRole").ValueKind);

        EnsureOrganizationMembershipExists();
    }

    [Fact]
    public async Task UpdateMe_WithValidToken_UpdatesProfile()
    {
        using var client = CreateClientWithToken(DemoCompanyId, DemoUserId, ["auth.me"]);

        using var response = await client.PutAsJsonAsync("/auth/me", new
        {
            fullName = "Updated Demo User",
            phoneNumber = "+254700001234",
            preferredLanguage = "fr",
            timeZone = "Africa/Nairobi",
            emailNotificationsEnabled = true,
            securityNotificationsEnabled = true,
            productUpdatesEnabled = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = payload.RootElement.GetProperty("data");
        Assert.Equal("Updated Demo User", data.GetProperty("fullName").GetString());
        Assert.Equal("fr", data.GetProperty("preferredLanguage").GetString());
        Assert.Equal("Africa/Nairobi", data.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task PasswordReset_ForUnknownEmail_ReturnsGenericSuccess()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/auth/password-reset", new
        {
            email = "missing@example.test"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("If the account exists, a password reset token has been generated.", payload.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Organizations_WithMultipleMemberships_ReturnsOnlyAuthorizedOrganizationsAndMarksActive()
    {
        EnsureOrganizationAccessFixture();

        using var client = CreateClientWithToken(DemoCompanyId, DemoUserId, ["auth.me"]);

        using var response = await client.GetAsync("/auth/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = payload.RootElement.GetProperty("data").EnumerateArray().ToList();

        Assert.Contains(items, item => item.GetProperty("companyId").GetGuid() == DemoCompanyId && item.GetProperty("isActiveOrganization").GetBoolean());
        Assert.Contains(items, item => item.GetProperty("companyId").GetGuid() == AccessibleCompanyId && !item.GetProperty("isActiveOrganization").GetBoolean());
        Assert.DoesNotContain(items, item => item.GetProperty("companyId").GetGuid() == UnauthorizedCompanyId);
        Assert.DoesNotContain(items, item => item.GetProperty("companyId").GetGuid() == InactiveMembershipCompanyId);
    }

    [Fact]
    public async Task SwitchOrganization_WithUnauthorizedOrganization_ReturnsForbidden()
    {
        EnsureOrganizationAccessFixture();

        using var client = CreateClientWithToken(DemoCompanyId, DemoUserId, ["auth.me"]);

        using var response = await client.PostAsJsonAsync("/auth/organizations/switch", new
        {
            companyId = UnauthorizedCompanyId
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Active organization membership is required.", payload.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task SwitchOrganization_WithAuthorizedOrganization_UpdatesCurrentCompanyAndIssuedToken()
    {
        EnsureOrganizationAccessFixture();

        using var client = CreateClientWithToken(DemoCompanyId, DemoUserId, ["auth.me"]);

        using var response = await client.PostAsJsonAsync("/auth/organizations/switch", new
        {
            companyId = AccessibleCompanyId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = payload.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        Assert.NotNull(token);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(AccessibleCompanyId.ToString("D"), jwt.Claims.First(claim => claim.Type == "company_id").Value);

        using var switchedClient = factory.CreateClient();
        switchedClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var meResponse = await switchedClient.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var mePayload = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
        Assert.Equal(AccessibleCompanyId, mePayload.RootElement.GetProperty("data").GetProperty("companyId").GetGuid());

        using var organizationResponse = await switchedClient.GetAsync("/organization/current");
        Assert.Equal(HttpStatusCode.OK, organizationResponse.StatusCode);
        var organizationPayload = JsonDocument.Parse(await organizationResponse.Content.ReadAsStringAsync());
        Assert.Equal(AccessibleCompanyId, organizationPayload.RootElement.GetProperty("data").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Organizations_ForLegacyUserWithCompanyIdButMissingMembership_RepairsCurrentCompanyMembership()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = dbContext.Users.Single(item => item.Id == DemoUserId);
        user.CompanyId = DemoCompanyId;

        var demoMembership = dbContext.OrganizationMembers
            .FirstOrDefault(item => item.CompanyId == DemoCompanyId && item.UserId == DemoUserId);
        if (demoMembership is not null)
        {
            dbContext.OrganizationMembers.Remove(demoMembership);
        }

        var company = dbContext.Companies.Single(item => item.Id == DemoCompanyId);
        company.OwnerUserId = DemoUserId;
        dbContext.SaveChanges();

        using var client = CreateClientWithToken(DemoCompanyId, DemoUserId, ["auth.me"]);

        using var response = await client.GetAsync("/auth/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = payload.RootElement.GetProperty("data").EnumerateArray().ToList();

        Assert.Contains(items, item => item.GetProperty("companyId").GetGuid() == DemoCompanyId && item.GetProperty("isActiveOrganization").GetBoolean());

        var repairedMembership = dbContext.OrganizationMembers.Single(item => item.CompanyId == DemoCompanyId && item.UserId == DemoUserId && item.Status == OrganizationMemberStatus.Active.ToStorageValue());
        Assert.Equal(OrganizationRole.Owner.ToStorageValue(), repairedMembership.Role);
    }

    [Fact]
    public async Task CreateOrganization_CreatesOwnerMembership_OnboardingProgress_FreeSubscription_AndSwitchesContext()
    {
        EnsureOrganizationAccessFixture();
        ResetDemoUserCompany(DemoCompanyId);
        var organizationName = $"Orbit Ops {Guid.NewGuid():N}"[..18];

        using var client = CreateClientWithToken(DemoCompanyId, DemoUserId, ["auth.me"]);

        using var response = await client.PostAsJsonAsync("/auth/organizations", new
        {
            name = organizationName,
            supportContactEmail = "orbit@example.test",
            countryCode = "KE",
            timeZone = "Africa/Nairobi"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = payload.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        Assert.NotNull(token);

        using var switchedClient = factory.CreateClient();
        switchedClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var meResponse = await switchedClient.GetAsync("/auth/me");
        var mePayload = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
        var createdCompanyId = mePayload.RootElement.GetProperty("data").GetProperty("companyId").GetGuid();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var company = dbContext.Companies.Single(item => item.Id == createdCompanyId);
        var membership = dbContext.OrganizationMembers.Single(item => item.CompanyId == createdCompanyId && item.UserId == DemoUserId);
        var onboardingProgress = dbContext.OnboardingProgressRecords.Single(item => item.CompanyId == createdCompanyId && item.UserId == DemoUserId);
        var subscription = dbContext.TenantSubscriptions
            .Include(item => item.SubscriptionPlan)
            .Single(item => item.CompanyId == createdCompanyId);

        Assert.Equal(DemoUserId, company.OwnerUserId);
        Assert.Equal(OrganizationRole.Owner.ToStorageValue(), membership.Role);
        Assert.Equal(OrganizationMemberStatus.Active.ToStorageValue(), membership.Status);
        Assert.Equal(OnboardingStep.Welcome.ToStorageValue(), onboardingProgress.CurrentStep);
        Assert.False(onboardingProgress.IsCompleted);
        Assert.Equal("Free", subscription.SubscriptionPlan.DisplayName);
    }

    private HttpClient CreateClientWithToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var client = factory.CreateClient();
        var token = CreateJwtToken(companyId, userId, permissions);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private void EnsureOrganizationAccessFixture()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        EnsureCompany(dbContext, AccessibleCompanyId, "Accessible Org");
        EnsureCompany(dbContext, UnauthorizedCompanyId, "Unauthorized Org");
        EnsureCompany(dbContext, InactiveMembershipCompanyId, "Inactive Membership Org");

        EnsureActiveMembership(dbContext, DemoCompanyId, DemoUserId, OrganizationRole.Owner.ToStorageValue());
        EnsureActiveMembership(dbContext, AccessibleCompanyId, DemoUserId, OrganizationRole.Manager.ToStorageValue());
        EnsureInactiveMembership(dbContext, InactiveMembershipCompanyId, DemoUserId, OrganizationRole.Host.ToStorageValue());

        ResetDemoUserCompany(DemoCompanyId);
        dbContext.SaveChanges();
    }

    private static void EnsureCompany(ApplicationDbContext dbContext, Guid companyId, string name)
    {
        if (dbContext.Companies.Any(item => item.Id == companyId))
        {
            return;
        }

        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            NormalizedSlug = name.ToUpperInvariant().Replace(' ', '-'),
            Email = $"{companyId:N}@example.test",
            PhoneNumber = "+254700000777",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            Status = "Active",
            IsActive = true
        });
    }

    private static void EnsureActiveMembership(ApplicationDbContext dbContext, Guid companyId, Guid userId, string role)
    {
        var membership = dbContext.OrganizationMembers.FirstOrDefault(item => item.CompanyId == companyId && item.UserId == userId);
        if (membership is null)
        {
            dbContext.OrganizationMembers.Add(new OrganizationMember
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = userId,
                Role = role,
                Status = OrganizationMemberStatus.Active.ToStorageValue(),
                JoinedAt = DateTimeOffset.UtcNow.AddDays(-7)
            });
            return;
        }

        membership.Role = role;
        membership.Status = OrganizationMemberStatus.Active.ToStorageValue();
    }

    private static void EnsureInactiveMembership(ApplicationDbContext dbContext, Guid companyId, Guid userId, string role)
    {
        var membership = dbContext.OrganizationMembers.FirstOrDefault(item => item.CompanyId == companyId && item.UserId == userId);
        if (membership is null)
        {
            dbContext.OrganizationMembers.Add(new OrganizationMember
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = userId,
                Role = role,
                Status = OrganizationMemberStatus.Removed.ToStorageValue(),
                JoinedAt = DateTimeOffset.UtcNow.AddDays(-7)
            });
            return;
        }

        membership.Role = role;
        membership.Status = OrganizationMemberStatus.Removed.ToStorageValue();
    }

    private void ResetDemoUserCompany(Guid companyId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = dbContext.Users.Single(item => item.Id == DemoUserId);
        user.CompanyId = companyId;
        dbContext.SaveChanges();
    }

    private void EnsureUserExists()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = dbContext.Users.FirstOrDefault(item => item.Id == DemoUserId);
        if (user is null)
        {
            user = new User
            {
                Id = DemoUserId,
                CompanyId = DemoCompanyId,
                FullName = "Demo User",
                Email = "demo.user@stayflow.local",
                PhoneNumber = "+254700000001",
                PreferredLanguage = "en",
                TimeZone = "UTC",
                Role = "Owner",
                PasswordHash = "integration-only-placeholder",
                IsEmailVerified = true,
                IsActive = true,
                EmailNotificationsEnabled = true,
                SecurityNotificationsEnabled = true,
                ProductUpdatesEnabled = false
            };
            dbContext.Users.Add(user);
        }

        var role = dbContext.Roles.FirstOrDefault(item => item.Name == "HostWorkspaceAdmin");
        if (role is null)
        {
            var permission = new Permission { Id = Guid.NewGuid(), Name = "auth.me" };
            dbContext.Permissions.Add(permission);
            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = "HostWorkspaceAdmin",
                RolePermissions =
                [
                    new RolePermission
                    {
                        Permission = permission,
                        PermissionId = permission.Id
                    }
                ]
            };
            dbContext.Roles.Add(role);
        }

        if (!dbContext.UserRoles.Any(item => item.UserId == DemoUserId && item.RoleId == role.Id))
        {
            dbContext.UserRoles.Add(new UserRole
            {
                UserId = DemoUserId,
                RoleId = role.Id
            });
        }

        if (!dbContext.OrganizationMembers.Any(item => item.CompanyId == DemoCompanyId && item.UserId == DemoUserId && item.Status == OrganizationMemberStatus.Active.ToStorageValue()))
        {
            dbContext.OrganizationMembers.Add(new OrganizationMember
            {
                Id = Guid.NewGuid(),
                CompanyId = DemoCompanyId,
                UserId = DemoUserId,
                Role = OrganizationRole.Owner.ToStorageValue(),
                Status = OrganizationMemberStatus.Active.ToStorageValue(),
                JoinedAt = DateTimeOffset.UtcNow.AddDays(-10)
            });
        }

        var company = dbContext.Companies.FirstOrDefault(item => item.Id == DemoCompanyId);
        if (company is not null)
        {
            company.OwnerUserId = DemoUserId;
        }

        dbContext.SaveChanges();
    }

    private void EnsureOrganizationMembershipExists()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!dbContext.OrganizationMembers.Any(item => item.CompanyId == DemoCompanyId && item.UserId == DemoUserId && item.Status == OrganizationMemberStatus.Active.ToStorageValue()))
        {
            dbContext.OrganizationMembers.Add(new OrganizationMember
            {
                Id = Guid.NewGuid(),
                CompanyId = DemoCompanyId,
                UserId = DemoUserId,
                Role = OrganizationRole.Owner.ToStorageValue(),
                Status = OrganizationMemberStatus.Active.ToStorageValue(),
                JoinedAt = DateTimeOffset.UtcNow.AddDays(-10)
            });
            dbContext.SaveChanges();
        }
    }

    private static string CreateJwtToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new("company_id", companyId.ToString("D")),
            new("session_id", Guid.NewGuid().ToString("D")),
            new(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new(ClaimTypes.Name, "Auth Integration Test")
        };

        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SignalRTestAppFactory.JwtSigningKey));
        var token = new JwtSecurityToken(
            issuer: SignalRTestAppFactory.JwtIssuer,
            audience: SignalRTestAppFactory.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}