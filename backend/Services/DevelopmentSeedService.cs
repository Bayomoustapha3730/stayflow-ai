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

        var role = await GetOrCreateDemoRoleAsync(cancellationToken);
        var demoUser = await GetOrCreateDemoUserAsync(role, demoPassword, cancellationToken);
        await EnsureDemoGuestAsync(cancellationToken);

        var currentDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await EnsureDemoReservationAsync(currentDate, cancellationToken);
        await EnsureDemoUserRoleAsync(demoUser.Id, role.Id, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetOrCreateDemoUserAsync(Role role, string demoPassword, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == DemoDemoUserId, cancellationToken);

        if (user is null)
        {
            user = new User { Id = DemoDemoUserId };
            dbContext.Users.Add(user);
        }

        user.CompanyId = SeedData.DemoCompanyId;
        user.Email = DemoUserEmail;
        user.FullName = DemoUserFullName;
        user.PhoneNumber = "+254700000001";
        user.PasswordHash = passwordHasher.HashPassword(demoPassword);
        user.IsEmailVerified = true;
        user.IsActive = true;
        user.Role = role.Name;

        return user;
    }

    private async Task EnsureDemoGuestAsync(CancellationToken cancellationToken)
    {
        var guest = await dbContext.Guests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == DemoDemoGuestId, cancellationToken);

        if (guest is null)
        {
            guest = new Guest { Id = DemoDemoGuestId };
            dbContext.Guests.Add(guest);
        }

        guest.CompanyId = SeedData.DemoCompanyId;
        guest.FirstName = "Demo";
        guest.LastName = "Guest";
        guest.Email = "demo.guest@stayflow.local";
        guest.PhoneNumber = "+254700000002";
        guest.PreferredLanguage = "en";
        guest.CountryCode = "KE";
        guest.IsActive = true;
        guest.IsDeleted = false;
        guest.DeletedAt = null;
        guest.DeletedBy = null;
    }

    private async Task EnsureDemoReservationAsync(DateOnly currentDate, CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Reservations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == DemoDemoReservationId, cancellationToken);

        if (reservation is null)
        {
            reservation = new Reservation { Id = DemoDemoReservationId };
            dbContext.Reservations.Add(reservation);
        }

        reservation.CompanyId = SeedData.DemoCompanyId;
        reservation.PropertyId = SeedData.DemoPropertyId;
        reservation.PrimaryGuestId = DemoDemoGuestId;
        reservation.ExternalReservationReference = DemoReservationReference;
        reservation.ReservationSource = "Airbnb";
        reservation.ConfirmationNumber = "DEMO-CONF-001";
        reservation.CheckInDate = currentDate.AddDays(-1);
        reservation.CheckOutDate = currentDate.AddDays(3);
        reservation.Adults = 2;
        reservation.Children = 0;
        reservation.TotalGuestCount = 2;
        reservation.Status = ReservationStatus.CheckedIn;
        reservation.Currency = "KES";
        reservation.BookingAmount = 5000.00m;
        reservation.SpecialRequests = "Demo reservation for StayFlow AI testing";
        reservation.IsActive = true;
        reservation.IsDeleted = false;
        reservation.DeletedAt = null;
        reservation.DeletedBy = null;
    }

    private async Task EnsureDemoUserRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.UserRoles
            .AnyAsync(userRole => userRole.UserId == userId && userRole.RoleId == roleId, cancellationToken);

        if (!exists)
        {
            dbContext.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId
            });
        }
    }

    private async Task<Role> GetOrCreateDemoRoleAsync(CancellationToken cancellationToken)
    {
        var existingRole = await dbContext.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == DemoDemoRoleId, cancellationToken);

        if (existingRole != null)
        {
            await EnsureRolePermissionsAsync(existingRole, cancellationToken);
            return existingRole;
        }

        // Create role with required permissions
        var requiredPermissions = RequiredPermissions();

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

    private async Task EnsureRolePermissionsAsync(Role role, CancellationToken cancellationToken)
    {
        var requiredPermissions = RequiredPermissions();

        foreach (var permissionName in requiredPermissions)
        {
            var permission = await dbContext.Permissions
                .FirstOrDefaultAsync(p => p.Name == permissionName, cancellationToken);

            if (permission is null)
            {
                permission = new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = permissionName
                };
                dbContext.Permissions.Add(permission);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var hasPermission = role.RolePermissions.Any(rolePermission => rolePermission.PermissionId == permission.Id);
            if (!hasPermission)
            {
                role.RolePermissions.Add(new RolePermission { PermissionId = permission.Id });
            }
        }
    }

    private static IReadOnlyCollection<string> RequiredPermissions()
    {
        return
        [
            "auth.me",
            "guests.read",
            "reservations.read",
            "ai.orchestrate",
            "conversations.read",
            "conversations.create",
            "conversations.reply",
            "conversations.escalate",
            "conversations.manage",
            "conversations.notes"
        ];
    }
}
