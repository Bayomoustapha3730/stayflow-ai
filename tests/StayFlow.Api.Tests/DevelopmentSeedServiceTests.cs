using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
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

    private const string DemoUserEmail = "demo.user@stayflow.local";
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

        var requiredPermissions = new[] { "auth.me", "guests.read", "reservations.read", "ai.orchestrate" };
        var rolePermissionNames = role.RolePermissions.Select(rp => rp.Permission.Name).ToList();

        foreach (var permission in requiredPermissions)
        {
            Assert.Contains(permission, rolePermissionNames);
        }
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
        // Should be CheckedIn or similar status that's eligible
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

    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new ApplicationDbContext(options);
        // Ensure seed data is applied
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
}
