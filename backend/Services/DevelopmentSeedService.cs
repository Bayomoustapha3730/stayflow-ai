using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ReservationContext;
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
    private static readonly Guid DemoPayReservationId = Guid.Parse("55555555-5555-5555-5555-555555555556");
    private static readonly Guid DemoPayPaymentId = Guid.Parse("77777777-7777-4777-8777-777777777771");
    private static readonly Guid DemoPayConversationId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccc01");
    private static readonly Guid DemoDemoRoleId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid DemoSubscriptionId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly Guid DemoWhatsAppIntegrationId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid DemoTemplateWelcomeGuestId = Guid.Parse("88888888-8888-4888-8888-888888888881");
    private static readonly Guid DemoTemplateBookingConfirmationId = Guid.Parse("88888888-8888-4888-8888-888888888882");
    private static readonly Guid DemoTemplateLateCheckoutId = Guid.Parse("88888888-8888-4888-8888-888888888883");
    private static readonly Guid DemoTemplateCheckinInstructionsId = Guid.Parse("88888888-8888-4888-8888-888888888884");
    private static readonly Guid DemoTemplateCheckoutReminderId = Guid.Parse("88888888-8888-4888-8888-888888888885");
    private static readonly Guid OnboardingTestCompanyId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private static readonly Guid OnboardingTestUserId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");

    private const string DemoUserEmail = "demo.user@stayflow.local";
    private const string DemoUserFullName = "Demo User";
    private const string DemoReservationReference = "DEMO-2026-001";
    private const string DemoPayReservationReference = "DEMO-PAY-002";
    private const string DemoPayReceiptNumber = "STAYFLOWDEVSEED001";
    private const string DemoRoleName = "Demo Administrator";
    private const string DemoOrganizationRole = nameof(OrganizationRole.Owner);
    private const string OnboardingTestUserEmail = "onboarding.user@stayflow.local";
    private const string OnboardingTestUserFullName = "Onboarding Test User";
    private const string OnboardingTestCompanyName = "StayFlow Onboarding Test";
    private const string OnboardingTestCompanySlug = "stayflow-onboarding-test";

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
        await EnsureDemoPayReservationAsync(currentDate, cancellationToken);
        await EnsureDemoSubscriptionAsync(cancellationToken);
        await EnsureDemoPropertyKnowledgeAsync(cancellationToken);
        await EnsureDemoWhatsAppIntegrationAsync(cancellationToken);
        await EnsureDemoWhatsAppTemplatesAsync(cancellationToken);
        await EnsureDemoOnboardingAsync(cancellationToken);
        await EnsureDemoUserRoleAsync(demoUser.Id, role.Id, cancellationToken);
        await EnsureDemoOrganizationMembershipAsync(demoUser.Id, cancellationToken);
        await EnsureDemoCompanyOwnershipAsync(demoUser.Id, cancellationToken);

        var onboardingPassword = configuration["DevelopmentSeed:OnboardingTestPassword"] ?? demoPassword;
        await EnsureOnboardingTestTenantAsync(role, onboardingPassword, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureOnboardingTestTenantAsync(Role role, string onboardingPassword, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == OnboardingTestCompanyId, cancellationToken);

        if (company is null)
        {
            company = new Company { Id = OnboardingTestCompanyId };
            dbContext.Companies.Add(company);
        }

        company.Name = OnboardingTestCompanyName;
        company.Slug = OnboardingTestCompanySlug;
        company.NormalizedSlug = OnboardingTestCompanySlug.ToUpperInvariant();
        company.Status = "Active";
        company.LegalName = "StayFlow Onboarding Test Ltd";
        company.Email = OnboardingTestUserEmail;
        company.PhoneNumber = "+254700000101";
        company.CountryCode = "KE";
        company.TimeZone = "Africa/Nairobi";
        company.IsActive = true;
        company.OnboardingState = OnboardingStep.Welcome.ToStorageValue();

        await dbContext.SaveChangesAsync(cancellationToken);

        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == OnboardingTestUserId, cancellationToken);

        if (user is null)
        {
            user = new User { Id = OnboardingTestUserId };
            dbContext.Users.Add(user);
        }

        user.CompanyId = OnboardingTestCompanyId;
        user.Email = OnboardingTestUserEmail;
        user.FullName = OnboardingTestUserFullName;
        user.PhoneNumber = "+254700000102";
        user.PasswordHash = passwordHasher.HashPassword(onboardingPassword);
        user.IsEmailVerified = true;
        user.IsActive = true;
        user.Role = DemoOrganizationRole;

        var membership = await dbContext.OrganizationMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(member => member.CompanyId == OnboardingTestCompanyId && member.UserId == user.Id, cancellationToken);

        if (membership is null)
        {
            membership = new OrganizationMember
            {
                Id = Guid.NewGuid(),
                CompanyId = OnboardingTestCompanyId,
                UserId = user.Id,
                JoinedAt = DateTimeOffset.UtcNow
            };
            dbContext.OrganizationMembers.Add(membership);
        }

        membership.Role = DemoOrganizationRole;
        membership.Status = OrganizationMemberStatus.Active.ToStorageValue();

        await EnsureDemoUserRoleAsync(user.Id, role.Id, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        company.OwnerUserId = user.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        var progressRows = await dbContext.OnboardingProgressRecords
            .Where(item => item.CompanyId == OnboardingTestCompanyId && item.UserId == OnboardingTestUserId)
            .ToListAsync(cancellationToken);
        if (progressRows.Count > 0)
        {
            dbContext.OnboardingProgressRecords.RemoveRange(progressRows);
        }

        var onboardingEvents = await dbContext.OnboardingEvents
            .Where(item => item.CompanyId == OnboardingTestCompanyId && item.UserId == OnboardingTestUserId)
            .ToListAsync(cancellationToken);
        if (onboardingEvents.Count > 0)
        {
            dbContext.OnboardingEvents.RemoveRange(onboardingEvents);
        }
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
        user.Role = DemoOrganizationRole;

        return user;
    }

    private async Task EnsureDemoOrganizationMembershipAsync(Guid userId, CancellationToken cancellationToken)
    {
        var membership = await dbContext.OrganizationMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(member => member.CompanyId == SeedData.DemoCompanyId && member.UserId == userId, cancellationToken);

        if (membership is null)
        {
            membership = new OrganizationMember
            {
                Id = Guid.NewGuid(),
                CompanyId = SeedData.DemoCompanyId,
                UserId = userId,
                JoinedAt = DateTimeOffset.UtcNow
            };
            dbContext.OrganizationMembers.Add(membership);
        }

        membership.Role = DemoOrganizationRole;
        membership.Status = OrganizationMemberStatus.Active.ToStorageValue();
        if (membership.JoinedAt == default)
        {
            membership.JoinedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task EnsureDemoCompanyOwnershipAsync(Guid userId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .FirstOrDefaultAsync(item => item.Id == SeedData.DemoCompanyId, cancellationToken);

        if (company is null)
        {
            return;
        }

        company.OwnerUserId = userId;
    }

    private async Task EnsureDemoOnboardingAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var progress = await dbContext.OnboardingProgressRecords
            .FirstOrDefaultAsync(item => item.CompanyId == SeedData.DemoCompanyId && item.UserId == DemoDemoUserId, cancellationToken);

        if (progress is null)
        {
            progress = new OnboardingProgress
            {
                Id = Guid.NewGuid(),
                CompanyId = SeedData.DemoCompanyId,
                UserId = DemoDemoUserId
            };
            dbContext.OnboardingProgressRecords.Add(progress);
        }

        progress.CurrentStep = OnboardingStep.Completed.ToStorageValue();
        progress.CompletedStepsCsv = string.Join(',', new[]
        {
            OnboardingStep.Welcome,
            OnboardingStep.OrganizationProfile,
            OnboardingStep.PlanConfirmation,
            OnboardingStep.FirstProperty,
            OnboardingStep.WhatsAppSetup,
            OnboardingStep.AiProviderSetup,
            OnboardingStep.KnowledgeBaseSetup,
            OnboardingStep.Review,
            OnboardingStep.Completed
        }.Select(step => step.ToStorageValue()));
        progress.SkippedStepsCsv = string.Join(',', new[]
        {
            OnboardingStep.TeamInvitations,
            OnboardingStep.DemoData
        }.Select(step => step.ToStorageValue()));
        progress.SelectedPlanName = "Starter";
        progress.FirstPropertyId = SeedData.DemoPropertyId;
        progress.IsCompleted = true;
        progress.CompletedAtUtc = now;
        progress.CompletedByUserId = DemoDemoUserId;
        progress.StartedAtUtc = progress.StartedAtUtc == default ? now : progress.StartedAtUtc;
        progress.LastUpdatedAtUtc = now;
        progress.Version = progress.Version <= 0 ? 1 : progress.Version + 1;

        var company = await dbContext.Companies
            .FirstOrDefaultAsync(item => item.Id == SeedData.DemoCompanyId, cancellationToken);

        if (company is not null)
        {
            company.OnboardingState = OnboardingStep.Completed.ToStorageValue();
        }
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
            reservation = new Reservation
            {
                Id = DemoDemoReservationId,
                Currency = "KES",
                BookingAmount = 5000.00m
            };

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
        reservation.SpecialRequests = "Demo reservation for StayFlow AI testing";
        reservation.IsActive = true;
        reservation.IsDeleted = false;
        reservation.DeletedAt = null;
        reservation.DeletedBy = null;
    }

    private async Task EnsureDemoPayReservationAsync(DateOnly currentDate, CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Reservations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == DemoPayReservationId, cancellationToken);

        if (reservation is null)
        {
            reservation = new Reservation
            {
                Id = DemoPayReservationId,
                Currency = "KES",
                BookingAmount = 4000.00m
            };

            dbContext.Reservations.Add(reservation);
        }

        reservation.CompanyId = SeedData.DemoCompanyId;
        reservation.PropertyId = SeedData.DemoPropertyId;
        reservation.PrimaryGuestId = DemoDemoGuestId;
        reservation.ExternalReservationReference = DemoPayReservationReference;
        reservation.ReservationSource = "DemoSeed";
        reservation.ConfirmationNumber = DemoPayReservationReference;
        reservation.CheckInDate = currentDate.AddDays(2);
        reservation.CheckOutDate = currentDate.AddDays(5);
        reservation.Adults = 2;
        reservation.Children = 0;
        reservation.TotalGuestCount = 2;
        reservation.Status = ReservationStatus.PreArrival;
        reservation.SpecialRequests = "Development-only payment seed for M-PESA phase 2 testing";
        reservation.IsActive = true;
        reservation.IsDeleted = false;
        reservation.DeletedAt = null;
        reservation.DeletedBy = null;

        await EnsureDemoPayConversationAsync(cancellationToken);
        await EnsureDemoPayPaymentAsync(cancellationToken);
    }

    private async Task EnsureDemoPayConversationAsync(CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == DemoPayConversationId, cancellationToken);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = DemoPayConversationId,
                CompanyId = SeedData.DemoCompanyId,
                GuestId = DemoDemoGuestId,
                ReservationId = DemoPayReservationId,
                PropertyId = SeedData.DemoPropertyId,
                Channel = GuestChannel.Web,
                ChannelIdentity = "demo-pay-seeded-web",
                Subject = "Demo M-PESA payment follow-up"
            };

            dbContext.Conversations.Add(conversation);
        }

        conversation.CompanyId = SeedData.DemoCompanyId;
        conversation.GuestId = DemoDemoGuestId;
        conversation.ReservationId = DemoPayReservationId;
        conversation.PropertyId = SeedData.DemoPropertyId;
        conversation.Channel = GuestChannel.Web;
        conversation.ChannelIdentity = "demo-pay-seeded-web";
        conversation.Status = ConversationStatus.Open;
        conversation.Subject = "Demo M-PESA payment follow-up";
        conversation.HumanTakeoverEnabled = true;
        conversation.IsDeleted = false;
        conversation.DeletedAt = null;
        conversation.DeletedBy = null;
        conversation.StartedAt = conversation.StartedAt == default ? DateTimeOffset.UtcNow : conversation.StartedAt;
        conversation.LastActivityAt = DateTimeOffset.UtcNow;
        conversation.ReservationContextBoundAt ??= DateTimeOffset.UtcNow;
    }

    private async Task EnsureDemoPayPaymentAsync(CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.Id == DemoPayPaymentId || p.ProviderTransactionId == DemoPayReceiptNumber, cancellationToken);

        if (payment is null)
        {
            payment = new Payment
            {
                Id = DemoPayPaymentId,
                CompanyId = SeedData.DemoCompanyId,
                PropertyId = SeedData.DemoPropertyId,
                GuestId = DemoDemoGuestId,
                ReservationId = DemoPayReservationId,
                Currency = "KES",
                Amount = 1000.00m,
                Provider = "M-PESA",
                ProviderEnvironment = "Sandbox",
                PaymentMethod = "STKPush",
                ProviderTransactionId = DemoPayReceiptNumber,
                Status = PaymentStatus.Paid.ToStorageValue(),
                RequestedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };

            dbContext.Payments.Add(payment);
        }

        payment.CompanyId = SeedData.DemoCompanyId;
        payment.PropertyId = SeedData.DemoPropertyId;
        payment.GuestId = DemoDemoGuestId;
        payment.ReservationId = DemoPayReservationId;
        payment.Currency = "KES";
        payment.Amount = 1000.00m;
        payment.Provider = "M-PESA";
        payment.ProviderEnvironment = "Sandbox";
        payment.PaymentMethod = "STKPush";
        payment.ProviderTransactionId = DemoPayReceiptNumber;
        payment.Status = PaymentStatus.Paid.ToStorageValue();
        payment.RequestedAtUtc ??= DateTimeOffset.UtcNow;
        payment.CompletedAtUtc ??= DateTimeOffset.UtcNow;
        payment.FailureCode = null;
        payment.FailureMessage = null;
        payment.FailedAtUtc = null;
        payment.CancelledAtUtc = null;
        payment.ExternalReference ??= DemoPayReservationReference;
        payment.InternalReference ??= DemoPayReservationReference;
        payment.CustomerPhoneNumber ??= "+254700000002";
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

    private async Task EnsureDemoSubscriptionAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1).AddTicks(-1);

        var activeStatuses = new[]
        {
            SubscriptionStatus.Active.ToStorageValue(),
            SubscriptionStatus.Trialing.ToStorageValue(),
            SubscriptionStatus.PastDue.ToStorageValue(),
            SubscriptionStatus.CancelAtPeriodEnd.ToStorageValue()
        };

        var subscriptions = await dbContext.TenantSubscriptions
            .Where(item => item.CompanyId == SeedData.DemoCompanyId)
            .ToListAsync(cancellationToken);
        subscriptions = subscriptions
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .ToList();

        var primary = subscriptions.FirstOrDefault(item => item.Id == DemoSubscriptionId)
            ?? subscriptions.FirstOrDefault(item => activeStatuses.Contains(item.Status));

        if (primary is null)
        {
            primary = new TenantSubscription
            {
                Id = DemoSubscriptionId,
                CompanyId = SeedData.DemoCompanyId
            };

            dbContext.TenantSubscriptions.Add(primary);
        }

        primary.SubscriptionPlanId = SeedData.StarterPlanId;
        primary.Status = SubscriptionStatus.Active.ToStorageValue();
        primary.CancelAtPeriodEnd = false;
        primary.EndedAtUtc = null;
        primary.CurrentPeriodStartUtc = periodStart;
        primary.CurrentPeriodEndUtc = periodEnd;
        primary.TrialEndsAtUtc = null;
        primary.Notes = "Development seed: demo tenant pinned to Starter plan for WhatsApp-enabled validation.";

        foreach (var subscription in subscriptions)
        {
            if (subscription.Id == primary.Id)
            {
                continue;
            }

            if (!activeStatuses.Contains(subscription.Status))
            {
                continue;
            }

            subscription.Status = SubscriptionStatus.Cancelled.ToStorageValue();
            subscription.CancelAtPeriodEnd = false;
            subscription.EndedAtUtc = now;
            subscription.Notes = "Development seed: superseded by deterministic demo Starter subscription.";
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

        var isNew = integration is null;
        if (integration is null)
        {
            integration = new WhatsAppIntegration { Id = DemoWhatsAppIntegrationId, IsDemoSeeded = true };
            dbContext.WhatsAppIntegrations.Add(integration);
        }

        integration.CompanyId = SeedData.DemoCompanyId;

        // Once an operator deliberately configures this integration (IsDemoSeeded=false), the seed
        // must never overwrite their routing metadata on subsequent startups.
        if (isNew || integration.IsDemoSeeded)
        {
            integration.DisplayName = "Demo WhatsApp Concierge";
            integration.PhoneNumberId = "demo-phone-number-id";
            integration.WhatsAppBusinessAccountId = "demo-waba-id";
            integration.BusinessPhoneNumberMasked = "+1******0002";
            integration.IsActive = true;
            integration.IsDemoSeeded = true;
        }
    }

    private async Task EnsureDemoWhatsAppTemplatesAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any(migration => migration.Contains("AddWhatsAppProductionTemplates", StringComparison.Ordinal)))
            {
                return;
            }
        }

        var seededTemplates = new[]
        {
            new
            {
                Id = DemoTemplateWelcomeGuestId,
                Name = "welcome_guest",
                LanguageCode = "en",
                Category = "UTILITY",
                Status = "APPROVED",
                HeaderType = (string?)"TEXT",
                BodyText = "Hello {{1}}, welcome to StayFlow. Your stay starts on {{2}}.",
                FooterText = (string?)"StayFlow Concierge",
                VariableCount = 2
            },
            new
            {
                Id = DemoTemplateBookingConfirmationId,
                Name = "booking_confirmation",
                LanguageCode = "fr",
                Category = "UTILITY",
                Status = "APPROVED",
                HeaderType = (string?)"TEXT",
                BodyText = "Bonjour {{1}}, votre reservation {{2}} est confirmee.",
                FooterText = (string?)"StayFlow Concierge",
                VariableCount = 2
            },
            new
            {
                Id = DemoTemplateLateCheckoutId,
                Name = "late_checkout",
                LanguageCode = "en",
                Category = "MARKETING",
                Status = "PENDING",
                HeaderType = (string?)null,
                BodyText = "Late checkout request for {{1}} is under review.",
                FooterText = (string?)null,
                VariableCount = 1
            },
            new
            {
                Id = DemoTemplateCheckinInstructionsId,
                Name = "checkin_instructions",
                LanguageCode = "es",
                Category = "AUTHENTICATION",
                Status = "APPROVED",
                HeaderType = (string?)"TEXT",
                BodyText = "Hola {{1}}, usa el codigo {{2}} para el check-in.",
                FooterText = (string?)"StayFlow Concierge",
                VariableCount = 2
            },
            new
            {
                Id = DemoTemplateCheckoutReminderId,
                Name = "checkout_reminder",
                LanguageCode = "en",
                Category = "UTILITY",
                Status = "REJECTED",
                HeaderType = (string?)null,
                BodyText = "Reminder: checkout is at {{1}}.",
                FooterText = (string?)null,
                VariableCount = 1
            }
        };

        foreach (var item in seededTemplates)
        {
            // Upsert by tenant + integration + template identity so reruns are deterministic and duplicate-safe.
            var template = await dbContext.WhatsAppTemplates
                .FirstOrDefaultAsync(t =>
                    t.CompanyId == SeedData.DemoCompanyId
                    && t.WhatsAppIntegrationId == DemoWhatsAppIntegrationId
                    && t.Name == item.Name
                    && t.LanguageCode == item.LanguageCode,
                    cancellationToken);

            if (template is null)
            {
                template = await dbContext.WhatsAppTemplates
                    .FirstOrDefaultAsync(t => t.Id == item.Id, cancellationToken);
            }

            if (template is null)
            {
                template = new WhatsAppTemplate { Id = item.Id };
                dbContext.WhatsAppTemplates.Add(template);
            }

            template.CompanyId = SeedData.DemoCompanyId;
            template.WhatsAppIntegrationId = DemoWhatsAppIntegrationId;
            template.ExternalTemplateId = $"dev-seeded-{item.Name}-{item.LanguageCode}";
            template.Name = item.Name;
            template.LanguageCode = item.LanguageCode;
            template.Category = item.Category;
            template.Status = item.Status;
            template.HeaderType = item.HeaderType;
            template.BodyText = item.BodyText;
            template.FooterText = item.FooterText;
            template.VariableCount = item.VariableCount;
            template.ComponentsJson = "{\"source\":\"development-seed\"}";
            template.LastSyncedAt = DateTimeOffset.UtcNow;
            template.IsActive = true;
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
