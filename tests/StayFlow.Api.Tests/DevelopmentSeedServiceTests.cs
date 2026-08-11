using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class DevelopmentSeedServiceTests
{
    private static readonly Guid DemoDemoUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DemoDemoGuestId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid DemoDemoReservationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid OnboardingTestCompanyId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private static readonly Guid OnboardingTestUserId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private const string DemoOrganizationRole = "Owner";

    private const string DemoUserEmail = "demo.user@stayflow.local";
    private const string DemoUserFullName = "Demo User";
    private const string OnboardingTestUserEmail = "onboarding.user@stayflow.local";
    private const string TestPassword = "TestPassword123!";

    [Fact]
    public async Task SeedAsync_WithoutConfiguredPassword_DoesNotCreateDemoUser()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?>());

        await seeder.SeedAsync(CancellationToken.None);

        var demoUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == DemoDemoUserId);
        Assert.Null(demoUser);
    }

    [Fact]
    public async Task SeedAsync_WithConfiguredPassword_CreatesDemoUser()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == DemoDemoUserId);
        Assert.NotNull(demoUser);
        Assert.Equal(DemoUserEmail, demoUser.Email);
    }

    [Fact]
    public async Task SeedAsync_CreatesUserWithHashedPassword_NotPlaintext()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == DemoDemoUserId);
        Assert.NotNull(demoUser);
        Assert.NotEqual(TestPassword, demoUser.PasswordHash);
        Assert.NotEmpty(demoUser.PasswordHash);
    }

    [Fact]
    public async Task SeedAsync_DemoUserBelongsToDemoCompany()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == DemoDemoUserId);
        Assert.NotNull(demoUser);
        Assert.Equal(SeedData.DemoCompanyId, demoUser.CompanyId);
    }

    [Fact]
    public async Task SeedAsync_DemoUserHasRequiredRoleAndPermissions()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoUser = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == DemoDemoUserId);

        Assert.NotNull(demoUser);
        var userRole = Assert.Single(demoUser.UserRoles);
        var role = userRole.Role;
        Assert.NotNull(role);

        var requiredPermissions = new[]
        {
            "auth.me",
            "properties.read",
            "properties.manage",
            "properties.approve",
            "guests.read",
            "reservations.read",
            "ai.orchestrate"
        };
        var rolePermissionNames = role.RolePermissions.Select(rp => rp.Permission.Name).ToList();

        foreach (var permission in requiredPermissions)
        {
            Assert.Contains(permission, rolePermissionNames);
        }
    }

    [Fact]
    public async Task SeedAsync_DemoUserHasActiveOrganizationMembershipAsOwner()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var membership = await dbContext.OrganizationMembers
            .SingleOrDefaultAsync(item => item.CompanyId == SeedData.DemoCompanyId && item.UserId == DemoDemoUserId);

        Assert.NotNull(membership);
        Assert.Equal(DemoOrganizationRole, membership.Role);
        Assert.Equal(OrganizationMemberStatus.Active.ToStorageValue(), membership.Status);
    }

    [Fact]
    public async Task SeedAsync_AssignsDemoUserAsDemoCompanyOwner()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var company = await dbContext.Companies.SingleAsync(item => item.Id == SeedData.DemoCompanyId);
        Assert.Equal(DemoDemoUserId, company.OwnerUserId);
    }

    [Fact]
    public async Task SeedAsync_CreatesDemoGuest()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoGuest = await dbContext.Guests.FirstOrDefaultAsync(g => g.Id == DemoDemoGuestId);
        Assert.NotNull(demoGuest);
        Assert.Equal("Demo", demoGuest.FirstName);
        Assert.Equal("Guest", demoGuest.LastName);
    }

    [Fact]
    public async Task SeedAsync_DemoGuestBelongsToDemoCompany()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoGuest = await dbContext.Guests.FirstOrDefaultAsync(g => g.Id == DemoDemoGuestId);
        Assert.NotNull(demoGuest);
        Assert.Equal(SeedData.DemoCompanyId, demoGuest.CompanyId);
    }

    [Fact]
    public async Task SeedAsync_CreatesDemoReservation()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoReservation = await dbContext.Reservations.FirstOrDefaultAsync(r => r.Id == DemoDemoReservationId);
        Assert.NotNull(demoReservation);
        Assert.Equal("DEMO-2026-001", demoReservation.ExternalReservationReference);
    }

    [Fact]
    public async Task SeedAsync_DemoReservationBelongsToDemoCompany()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoReservation = await dbContext.Reservations.FirstOrDefaultAsync(r => r.Id == DemoDemoReservationId);
        Assert.NotNull(demoReservation);
        Assert.Equal(SeedData.DemoCompanyId, demoReservation.CompanyId);
    }

    [Fact]
    public async Task SeedAsync_DemoReservationUsesDemoProperty()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoReservation = await dbContext.Reservations.FirstOrDefaultAsync(r => r.Id == DemoDemoReservationId);
        Assert.NotNull(demoReservation);
        Assert.Equal(SeedData.DemoPropertyId, demoReservation.PropertyId);
    }

    [Fact]
    public async Task SeedAsync_DemoReservationUseDemoDemoGuest()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoReservation = await dbContext.Reservations.FirstOrDefaultAsync(r => r.Id == DemoDemoReservationId);
        Assert.NotNull(demoReservation);
        Assert.Equal(DemoDemoGuestId, demoReservation.PrimaryGuestId);
    }

    [Fact]
    public async Task SeedAsync_DemoReservationIsEligibleForAiContextResolution()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        var demoReservation = await dbContext.Reservations.FirstOrDefaultAsync(r => r.Id == DemoDemoReservationId);
        Assert.NotNull(demoReservation);
        Assert.Equal(ReservationStatus.CheckedIn, demoReservation.Status);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_RunningTwiceDoesNotDuplicateData()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);
        var firstRunUserCount = await dbContext.Users.CountAsync(u => u.Id == DemoDemoUserId);
        var firstRunGuestCount = await dbContext.Guests.CountAsync(g => g.Id == DemoDemoGuestId);
        var firstRunReservationCount = await dbContext.Reservations.CountAsync(r => r.Id == DemoDemoReservationId);

        await seeder.SeedAsync(CancellationToken.None);
        var secondRunUserCount = await dbContext.Users.CountAsync(u => u.Id == DemoDemoUserId);
        var secondRunGuestCount = await dbContext.Guests.CountAsync(g => g.Id == DemoDemoGuestId);
        var secondRunReservationCount = await dbContext.Reservations.CountAsync(r => r.Id == DemoDemoReservationId);

        Assert.Equal(firstRunUserCount, secondRunUserCount);
        Assert.Equal(firstRunGuestCount, secondRunGuestCount);
        Assert.Equal(firstRunReservationCount, secondRunReservationCount);
    }

    [Fact]
    public async Task SeedAsync_UpdatesPasswordHashOnSubsequentRunsWithDifferentPassword()
    {
        var dbContext = CreateInMemoryDbContext();
        var hasher = new Pbkdf2PasswordHasher();
        var config1 = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = "Password1" })
            .Build();
        var seeder1 = new DevelopmentSeedService(dbContext, hasher, config1);

        await seeder1.SeedAsync(CancellationToken.None);
        var user1 = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == DemoDemoUserId);
        var passwordHash1 = user1!.PasswordHash;

        var config2 = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = "Password2" })
            .Build();
        var seeder2 = new DevelopmentSeedService(dbContext, hasher, config2);

        await seeder2.SeedAsync(CancellationToken.None);
        var user2 = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == DemoDemoUserId);
        var passwordHash2 = user2!.PasswordHash;

        Assert.NotEqual(passwordHash1, passwordHash2);
    }

    [Fact]
    public async Task SeedAsync_WhenDemoUserAlreadyExists_StillCreatesGuestReservationAndRole()
    {
        var dbContext = CreateInMemoryDbContext();
        dbContext.Users.Add(new User
        {
            Id = DemoDemoUserId,
            CompanyId = SeedData.DemoCompanyId,
            Email = DemoUserEmail,
            FullName = "Existing Demo",
            PasswordHash = "old-hash",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });

        await seeder.SeedAsync(CancellationToken.None);

        Assert.NotNull(await dbContext.Guests.FirstOrDefaultAsync(g => g.Id == DemoDemoGuestId));
        Assert.NotNull(await dbContext.Reservations.FirstOrDefaultAsync(r => r.Id == DemoDemoReservationId));
        Assert.True(await dbContext.UserRoles.AnyAsync(userRole => userRole.UserId == DemoDemoUserId));
        Assert.True(await dbContext.OrganizationMembers.AnyAsync(member => member.CompanyId == SeedData.DemoCompanyId
            && member.UserId == DemoDemoUserId
            && member.Role == DemoOrganizationRole
            && member.Status == OrganizationMemberStatus.Active.ToStorageValue()));
    }

    [Fact]
    public async Task SeedAsync_RepairsExistingDemoUserMissingOrganizationMembership()
    {
        var dbContext = CreateInMemoryDbContext();
        dbContext.Users.Add(new User
        {
            Id = DemoDemoUserId,
            CompanyId = SeedData.DemoCompanyId,
            Email = DemoUserEmail,
            FullName = DemoUserFullName,
            PasswordHash = "old-hash",
            Role = "Administrator",
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });
        await seeder.SeedAsync(CancellationToken.None);

        var membership = await dbContext.OrganizationMembers.SingleAsync(item => item.CompanyId == SeedData.DemoCompanyId && item.UserId == DemoDemoUserId);
        Assert.Equal(DemoOrganizationRole, membership.Role);
        Assert.Equal(OrganizationMemberStatus.Active.ToStorageValue(), membership.Status);

        var company = await dbContext.Companies.SingleAsync(item => item.Id == SeedData.DemoCompanyId);
        Assert.Equal(DemoDemoUserId, company.OwnerUserId);
    }

    [Fact]
    public async Task SeedAsync_RepairsExistingDemoMembershipWithoutCreatingDuplicate()
    {
        var dbContext = CreateInMemoryDbContext();
        dbContext.Users.Add(new User
        {
            Id = DemoDemoUserId,
            CompanyId = SeedData.DemoCompanyId,
            Email = DemoUserEmail,
            FullName = DemoUserFullName,
            PasswordHash = "old-hash",
            Role = "Support",
            IsActive = true
        });
        dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            Id = Guid.NewGuid(),
            CompanyId = SeedData.DemoCompanyId,
            UserId = DemoDemoUserId,
            Role = OrganizationRole.Support.ToStorageValue(),
            Status = OrganizationMemberStatus.Removed.ToStorageValue(),
            JoinedAt = DateTimeOffset.UtcNow.AddDays(-30)
        });
        await dbContext.SaveChangesAsync();

        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });
        await seeder.SeedAsync(CancellationToken.None);

        var memberships = await dbContext.OrganizationMembers
            .Where(item => item.CompanyId == SeedData.DemoCompanyId && item.UserId == DemoDemoUserId)
            .ToListAsync();

        var membership = Assert.Single(memberships);
        Assert.Equal(DemoOrganizationRole, membership.Role);
        Assert.Equal(OrganizationMemberStatus.Active.ToStorageValue(), membership.Status);
    }

    [Fact]
    public async Task SeedAsync_RepairsStaleDemoReservationForAiContextResolution()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?> { ["DevelopmentSeed:DemoPassword"] = TestPassword });
        await seeder.SeedAsync(CancellationToken.None);

        var reservation = await dbContext.Reservations.FirstAsync(r => r.Id == DemoDemoReservationId);
        reservation.Status = ReservationStatus.Cancelled;
        reservation.IsActive = false;
        reservation.IsDeleted = true;
        reservation.CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);
        reservation.CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-20);
        await dbContext.SaveChangesAsync();

        await seeder.SeedAsync(CancellationToken.None);

        var repairedReservation = await dbContext.Reservations.FirstAsync(r => r.Id == DemoDemoReservationId);
        var currentDate = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(ReservationStatus.CheckedIn, repairedReservation.Status);
        Assert.True(repairedReservation.IsActive);
        Assert.False(repairedReservation.IsDeleted);
        Assert.True(repairedReservation.CheckInDate <= currentDate);
        Assert.True(repairedReservation.CheckOutDate >= currentDate);
        Assert.Equal(DemoDemoGuestId, repairedReservation.PrimaryGuestId);
        Assert.Equal(SeedData.DemoPropertyId, repairedReservation.PropertyId);
    }

    [Fact]
    public async Task SeedAsync_CreatesOnboardingTestIdentity_WithNotStartedOnboarding()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?>
        {
            ["DevelopmentSeed:DemoPassword"] = TestPassword,
            ["DevelopmentSeed:OnboardingTestPassword"] = TestPassword
        });

        await seeder.SeedAsync(CancellationToken.None);

        var company = await dbContext.Companies.SingleAsync(item => item.Id == OnboardingTestCompanyId);
        var user = await dbContext.Users.SingleAsync(item => item.Id == OnboardingTestUserId);
        var membership = await dbContext.OrganizationMembers.SingleAsync(item => item.CompanyId == OnboardingTestCompanyId && item.UserId == OnboardingTestUserId);

        Assert.Equal("StayFlow Onboarding Test", company.Name);
        Assert.Equal(OnboardingStep.Welcome.ToStorageValue(), company.OnboardingState);
        Assert.Equal(OnboardingTestUserEmail, user.Email);
        Assert.Equal(OnboardingTestCompanyId, user.CompanyId);
        Assert.Equal(DemoOrganizationRole, membership.Role);
        Assert.Equal(OrganizationMemberStatus.Active.ToStorageValue(), membership.Status);
        Assert.False(await dbContext.OnboardingProgressRecords.AnyAsync(item => item.CompanyId == OnboardingTestCompanyId && item.UserId == OnboardingTestUserId));
    }

    [Fact]
    public async Task SeedAsync_CreatesOnboardingTestIdentity_UsingRelationalProviderWithoutCircularDependency()
    {
        await using var fixture = await CreateRelationalFixtureAsync(new Dictionary<string, string?>
        {
            ["DevelopmentSeed:DemoPassword"] = TestPassword,
            ["DevelopmentSeed:OnboardingTestPassword"] = TestPassword
        });

        await fixture.Seeder.SeedAsync(CancellationToken.None);
        await fixture.Seeder.SeedAsync(CancellationToken.None);

        var company = await fixture.DbContext.Companies.SingleAsync(item => item.Id == OnboardingTestCompanyId);
        var user = await fixture.DbContext.Users.SingleAsync(item => item.Id == OnboardingTestUserId);
        var membership = await fixture.DbContext.OrganizationMembers.SingleAsync(item => item.CompanyId == OnboardingTestCompanyId && item.UserId == OnboardingTestUserId);

        Assert.Equal(OnboardingTestUserId, company.OwnerUserId);
        Assert.Equal(OnboardingTestCompanyId, user.CompanyId);
        Assert.Equal(DemoOrganizationRole, membership.Role);
        Assert.Equal(OrganizationMemberStatus.Active.ToStorageValue(), membership.Status);
        Assert.Equal(OnboardingStep.Welcome.ToStorageValue(), company.OnboardingState);
        Assert.False(await fixture.DbContext.OnboardingProgressRecords.AnyAsync(item => item.CompanyId == OnboardingTestCompanyId && item.UserId == OnboardingTestUserId));
        Assert.Equal(1, await fixture.DbContext.Companies.CountAsync(item => item.Id == OnboardingTestCompanyId));
        Assert.Equal(1, await fixture.DbContext.Users.CountAsync(item => item.Id == OnboardingTestUserId));
        Assert.Equal(1, await fixture.DbContext.OrganizationMembers.CountAsync(item => item.CompanyId == OnboardingTestCompanyId && item.UserId == OnboardingTestUserId));
    }

    [Fact]
    public async Task SeedAsync_OnboardingTestIdentity_DoesNotSeedOperationalResources()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?>
        {
            ["DevelopmentSeed:DemoPassword"] = TestPassword,
            ["DevelopmentSeed:OnboardingTestPassword"] = TestPassword
        });

        await seeder.SeedAsync(CancellationToken.None);

        Assert.False(await dbContext.Properties.AnyAsync(item => item.CompanyId == OnboardingTestCompanyId && !item.IsDeleted));
        Assert.False(await dbContext.TenantSubscriptions.AnyAsync(item => item.CompanyId == OnboardingTestCompanyId));
        Assert.False(await dbContext.WhatsAppIntegrations.AnyAsync(item => item.CompanyId == OnboardingTestCompanyId));
        Assert.False(await dbContext.PropertyKnowledgeArticles.AnyAsync(item => item.CompanyId == OnboardingTestCompanyId && !item.IsDeleted));
        Assert.False(await dbContext.Reservations.AnyAsync(item => item.CompanyId == OnboardingTestCompanyId && !item.IsDeleted));
    }

    [Fact]
    public async Task SeedAsync_OnboardingTestIdentity_RepairsPartiallySeededTenantSafely()
    {
        await using var fixture = await CreateRelationalFixtureAsync(new Dictionary<string, string?>
        {
            ["DevelopmentSeed:DemoPassword"] = TestPassword,
            ["DevelopmentSeed:OnboardingTestPassword"] = TestPassword
        });

        fixture.DbContext.Companies.Add(new Company
        {
            Id = OnboardingTestCompanyId,
            Name = "Partial Tenant",
            Slug = "partial-tenant",
            NormalizedSlug = "PARTIAL-TENANT",
            Status = "Active",
            Email = OnboardingTestUserEmail,
            PhoneNumber = "+254700000201",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Seeder.SeedAsync(CancellationToken.None);

        var company = await fixture.DbContext.Companies.SingleAsync(item => item.Id == OnboardingTestCompanyId);
        var user = await fixture.DbContext.Users.SingleAsync(item => item.Id == OnboardingTestUserId);

        Assert.Equal(OnboardingTestUserId, company.OwnerUserId);
        Assert.Equal(OnboardingTestCompanyId, user.CompanyId);
        Assert.Equal(OnboardingStep.Welcome.ToStorageValue(), company.OnboardingState);
        Assert.False(await fixture.DbContext.OnboardingProgressRecords.AnyAsync(item => item.CompanyId == OnboardingTestCompanyId && item.UserId == OnboardingTestUserId));
    }

    [Fact]
    public async Task SeedAsync_OnboardingTestIdentity_ResetsExistingOnboardingProgress()
    {
        var dbContext = CreateInMemoryDbContext();
        var seeder = CreateSeeder(dbContext, new Dictionary<string, string?>
        {
            ["DevelopmentSeed:DemoPassword"] = TestPassword,
            ["DevelopmentSeed:OnboardingTestPassword"] = TestPassword
        });

        await seeder.SeedAsync(CancellationToken.None);

        dbContext.OnboardingProgressRecords.Add(new OnboardingProgress
        {
            Id = Guid.NewGuid(),
            CompanyId = OnboardingTestCompanyId,
            UserId = OnboardingTestUserId,
            CurrentStep = OnboardingStep.Completed.ToStorageValue(),
            CompletedStepsCsv = "Welcome,OrganizationProfile,PlanConfirmation,FirstProperty,TeamInvitations,WhatsAppSetup,AiProviderSetup,KnowledgeBaseSetup,DemoData,Review",
            IsCompleted = true,
            StartedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            LastUpdatedAtUtc = DateTimeOffset.UtcNow,
            Version = 3
        });
        await dbContext.SaveChangesAsync();

        await seeder.SeedAsync(CancellationToken.None);

        Assert.False(await dbContext.OnboardingProgressRecords.AnyAsync(item => item.CompanyId == OnboardingTestCompanyId && item.UserId == OnboardingTestUserId));
    }

    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static DevelopmentSeedService CreateSeeder(ApplicationDbContext dbContext, Dictionary<string, string?> configValues)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var hasher = new Pbkdf2PasswordHasher();
        return new DevelopmentSeedService(dbContext, hasher, config);
    }

    private static async Task<RelationalFixture> CreateRelationalFixtureAsync(Dictionary<string, string?> configValues)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        var seeder = new DevelopmentSeedService(dbContext, new Pbkdf2PasswordHasher(), config);
        return new RelationalFixture(dbContext, seeder, connection);
    }

    private sealed class RelationalFixture(ApplicationDbContext dbContext, DevelopmentSeedService seeder, SqliteConnection connection) : IAsyncDisposable
    {
        public ApplicationDbContext DbContext { get; } = dbContext;
        public DevelopmentSeedService Seeder { get; } = seeder;
        private SqliteConnection Connection { get; } = connection;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
