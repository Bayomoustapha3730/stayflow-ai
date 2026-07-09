using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

/// <summary>
/// Development-only seeder for AI testing data.
/// Creates demo user, guest, and reservation when DevelopmentSeed:DemoPassword is configured.
/// </summary>
public sealed class DevelopmentSeedService(
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IConfiguration configuration) : IDevelopmentSeedService
{
    // Deterministic GUIDs for development data
    private static readonly Guid DemoDemoUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DemoDemoGuestId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid DemoDemoReservationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid DemoDemoRoleId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private const string DemoUserEmail = "demo.user@stayflow.local";
    private const string DemoUserFullName = "Demo User";
    private const string DemoReservationReference = "DEMO-2026-001";
    private const string DemoRoleName = "Demo Administrator";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var demoPassword = configuration["DevelopmentSeed:DemoPassword"];
        if (string.IsNullOrEmpty(demoPassword))
        {
            return; // No password configured, skip seeding
        }

        // Check if demo user already exists
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == DemoDemoUserId, cancellationToken);

        if (existingUser != null)
        {
            // Update password hash if needed (idempotent)
            var newPasswordHash = passwordHasher.HashPassword(demoPassword);
            existingUser.PasswordHash = newPasswordHash;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Create demo role with required permissions
        var role = await GetOrCreateDemoRoleAsync(cancellationToken);

        // Create demo user
        var demoUser = new User
        {
            Id = DemoDemoUserId,
            CompanyId = SeedData.DemoCompanyId,
            Email = DemoUserEmail,
            FullName = DemoUserFullName,
            PhoneNumber = "+254700000001",
            PasswordHash = passwordHasher.HashPassword(demoPassword),
            IsEmailVerified = true,
            IsActive = true,
            Role = role.Name
        };

        // Create demo guest
        var demoGuest = new Guest
        {
            Id = DemoDemoGuestId,
            CompanyId = SeedData.DemoCompanyId,
            FirstName = "Demo",
            LastName = "Guest",
            Email = "demo.guest@stayflow.local",
            PhoneNumber = "+254700000002",
            IsActive = true
        };

        // Create demo reservation (eligible for AI context resolution)
        // Use dates that are current relative to test date 2026-08-10
        var currentDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var checkInDate = currentDate.AddDays(-1); // Yesterday (pre-arrival to active stay)
        var checkOutDate = currentDate.AddDays(3);  // 3 days from now

        var demoReservation = new Reservation
        {
            Id = DemoDemoReservationId,
            CompanyId = SeedData.DemoCompanyId,
            PropertyId = SeedData.DemoPropertyId,
            PrimaryGuestId = DemoDemoGuestId,
            ExternalReservationReference = DemoReservationReference,
            ConfirmationNumber = "DEMO-CONF-001",
            CheckInDate = checkInDate,
            CheckOutDate = checkOutDate,
            Adults = 2,
            Children = 0,
            TotalGuestCount = 2,
            Status = ReservationStatus.CheckedIn, // Eligible for AI context resolution
            Currency = "KES",
            BookingAmount = 5000.00m,
            SpecialRequests = "Demo reservation for StayFlow AI testing",
            IsActive = true
        };

        // Assign role to user
        var userRole = new UserRole
        {
            UserId = DemoDemoUserId,
            RoleId = role.Id
        };

        dbContext.Users.Add(demoUser);
        dbContext.Guests.Add(demoGuest);
        dbContext.Reservations.Add(demoReservation);
        dbContext.UserRoles.Add(userRole);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Role> GetOrCreateDemoRoleAsync(CancellationToken cancellationToken)
    {
        var existingRole = await dbContext.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == DemoDemoRoleId, cancellationToken);

        if (existingRole != null)
        {
            return existingRole;
        }

        // Create role with required permissions
        var requiredPermissions = new[]
        {
            "auth.me",
            "guests.read",
            "reservations.read",
            "ai.orchestrate"
        };

        var permissions = new List<Permission>();
        foreach (var permissionName in requiredPermissions)
        {
            var existingPermission = await dbContext.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName, cancellationToken);

            if (existingPermission == null)
            {
                existingPermission = new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = permissionName
                };
                dbContext.Permissions.Add(existingPermission);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            permissions.Add(existingPermission);
        }

        var role = new Role
        {
            Id = DemoDemoRoleId,
            Name = DemoRoleName,
            Description = "Development-only role for demo user with AI testing permissions",
            IsActive = true,
            RolePermissions = permissions
                .Select(p => new RolePermission { PermissionId = p.Id })
                .ToList()
        };

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return role;
    }
}
