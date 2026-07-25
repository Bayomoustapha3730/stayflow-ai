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
    private static readonly Guid DemoWhatsAppIntegrationId = Guid.Parse("77777777-7777-7777-7777-777777777777");

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
        await EnsureDemoPropertyKnowledgeAsync(cancellationToken);
        await EnsureDemoWhatsAppIntegrationAsync(cancellationToken);
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

    private async Task EnsureDemoPropertyKnowledgeAsync(CancellationToken cancellationToken)
    {
        var items = new[]
        {
            (Title: "Guest Wi-Fi", Category: PropertyKnowledgeCategory.WiFi, Summary: "Demo Wi-Fi details for guests.", Content: "SSID: StayFlowGuest. Password: DemoStay2026.", Tags: "wifi,internet,network", Priority: 10),
            (Title: "Check-in details", Category: PropertyKnowledgeCategory.CheckIn, Summary: "Standard arrival guidance.", Content: "Standard check-in time is 3:00 PM. Access instructions are sent on arrival day.", Tags: "check-in,arrival,access", Priority: 9),
            (Title: "Checkout details", Category: PropertyKnowledgeCategory.Checkout, Summary: "Standard departure guidance.", Content: "Standard checkout time is 11:00 AM. Please message the host if you need clarification.", Tags: "checkout,departure", Priority: 8),
            (Title: "Parking", Category: PropertyKnowledgeCategory.Parking, Summary: "Parking rules for the demo property.", Content: "One designated parking space is available. Additional vehicles require host confirmation.", Tags: "parking,car,garage", Priority: 8),
            (Title: "House rules", Category: PropertyKnowledgeCategory.HouseRules, Summary: "Core guest house rules.", Content: "Quiet hours begin at 10:00 PM. No smoking is permitted inside the property.", Tags: "house-rules,quiet-hours", Priority: 7),
            (Title: "Emergency guidance", Category: PropertyKnowledgeCategory.Emergency, Summary: "Basic demo emergency instructions.", Content: "For urgent safety emergencies, contact local emergency services first and then notify the host through StayFlow.", Tags: "emergency,safety", Priority: 10),
            (Title: "Local recommendations", Category: PropertyKnowledgeCategory.LocalRecommendations, Summary: "Nearby demo recommendations.", Content: "Nearby options include demo cafes, grocery stores, and casual dining within a short drive.", Tags: "local,recommendations,food", Priority: 4)
        };

        foreach (var item in items)
        {
            var existing = await dbContext.PropertyKnowledgeArticles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(article => article.CompanyId == SeedData.DemoCompanyId
                    && article.PropertyId == SeedData.DemoPropertyId
                    && article.Title == item.Title,
                    cancellationToken);

            if (existing is null)
            {
                existing = new PropertyKnowledgeArticle { Id = Guid.NewGuid() };
                dbContext.PropertyKnowledgeArticles.Add(existing);
            }

            existing.CompanyId = SeedData.DemoCompanyId;
            existing.PropertyId = SeedData.DemoPropertyId;
            existing.Category = item.Category;
            existing.Title = item.Title;
            existing.Summary = item.Summary;
            existing.Content = item.Content;
            existing.Tags = item.Tags;
            existing.Priority = item.Priority;
            existing.IsApproved = true;
            existing.IsActive = true;
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedByUserId = null;
            existing.ApprovedAt = DateTimeOffset.UtcNow;
            existing.ApprovedByUserId = DemoDemoUserId;
            existing.CreatedByUserId = DemoDemoUserId;
            existing.UpdatedByUserId = DemoDemoUserId;
        }
    }

    private async Task EnsureDemoWhatsAppIntegrationAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any(migration => migration.Contains("AddWhatsAppMessagingFoundation", StringComparison.Ordinal)))
            {
                return;
            }
        }

        var integration = await dbContext.WhatsAppIntegrations
            .FirstOrDefaultAsync(item => item.Id == DemoWhatsAppIntegrationId, cancellationToken);

        if (integration is null)
        {
            integration = new WhatsAppIntegration { Id = DemoWhatsAppIntegrationId };
            dbContext.WhatsAppIntegrations.Add(integration);
        }

        integration.CompanyId = SeedData.DemoCompanyId;
        integration.DisplayName = "Demo WhatsApp Concierge";
        integration.PhoneNumberId = "demo-phone-number-id";
        integration.WhatsAppBusinessAccountId = "demo-waba-id";
        integration.BusinessPhoneNumberMasked = "+1******0002";
        integration.IsActive = true;
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
            "properties.read",
            "properties.manage",
            "properties.approve",
            "guests.read",
            "reservations.read",
            "ai.orchestrate",
            "conversations.read",
            "conversations.create",
            "conversations.reply",
            "conversations.escalate",
            "conversations.manage",
            "conversations.notes",
            "chat.send",
            "chat.read",
            "chat.escalate",
            "chat.end"
        ];
    }
}
