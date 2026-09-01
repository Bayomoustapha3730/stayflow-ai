using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Data.Configurations;
using StayFlow.Api.Services;

namespace StayFlow.Api.Data;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ITenantContext? tenantContext = null) : DbContext(options)
{
    private readonly ITenantContext? _tenantContext = tenantContext;

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<UsageOperation> UsageOperations => Set<UsageOperation>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();
    public DbSet<OnboardingProgress> OnboardingProgressRecords => Set<OnboardingProgress>();
    public DbSet<OnboardingEvent> OnboardingEvents => Set<OnboardingEvent>();
    public DbSet<BillingWebhookEvent> BillingWebhookEvents => Set<BillingWebhookEvent>();
    public DbSet<TenantInvoice> TenantInvoices => Set<TenantInvoice>();
    public DbSet<TenantApiKey> TenantApiKeys => Set<TenantApiKey>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyAmenity> PropertyAmenities => Set<PropertyAmenity>();
    public DbSet<PropertyHouseRule> PropertyHouseRules => Set<PropertyHouseRule>();
    public DbSet<PropertyRecommendation> PropertyRecommendations => Set<PropertyRecommendation>();
    public DbSet<PropertyEmergencyContact> PropertyEmergencyContacts => Set<PropertyEmergencyContact>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationLifecycleEvent> ReservationLifecycleEvents => Set<ReservationLifecycleEvent>();
    public DbSet<KnowledgeBaseItem> KnowledgeBaseItems => Set<KnowledgeBaseItem>();
    public DbSet<PropertyKnowledgeArticle> PropertyKnowledgeArticles => Set<PropertyKnowledgeArticle>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<ConversationMessageKnowledgeSource> ConversationMessageKnowledgeSources => Set<ConversationMessageKnowledgeSource>();
    public DbSet<ConversationMessageFeedback> ConversationMessageFeedback => Set<ConversationMessageFeedback>();
    public DbSet<ConversationParticipantReadState> ConversationParticipantReadStates => Set<ConversationParticipantReadState>();
    public DbSet<PendingConciergeAction> PendingConciergeActions => Set<PendingConciergeAction>();
    public DbSet<ConciergeActionAuditLog> ConciergeActionAuditLogs => Set<ConciergeActionAuditLog>();
    public DbSet<EarlyCheckInRequest> EarlyCheckInRequests => Set<EarlyCheckInRequest>();
    public DbSet<LateCheckoutRequest> LateCheckoutRequests => Set<LateCheckoutRequest>();
    public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
    public DbSet<HousekeepingRequest> HousekeepingRequests => Set<HousekeepingRequest>();
    public DbSet<ExtraItemRequest> ExtraItemRequests => Set<ExtraItemRequest>();
    public DbSet<ParkingRequest> ParkingRequests => Set<ParkingRequest>();
    public DbSet<HostNotificationRecord> HostNotificationRecords => Set<HostNotificationRecord>();
    public DbSet<ActionNotificationOutbox> ActionNotificationOutbox => Set<ActionNotificationOutbox>();
    public DbSet<HostCopilotSlaAlert> HostCopilotSlaAlerts => Set<HostCopilotSlaAlert>();
    public DbSet<WhatsAppIntegration> WhatsAppIntegrations => Set<WhatsAppIntegration>();
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    public DbSet<Models.ServiceProvider> ServiceProviders => Set<Models.ServiceProvider>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public override int SaveChanges()
    {
        ValidateTenantOwnership();
        UpdateAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateTenantOwnership();
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new SubscriptionPlanConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new PlanEntitlementConfiguration());
        modelBuilder.ApplyConfiguration(new UsageRecordConfiguration());
        modelBuilder.ApplyConfiguration(new UsageOperationConfiguration());
        modelBuilder.ApplyConfiguration(new OnboardingProgressConfiguration());
        modelBuilder.ApplyConfiguration(new OnboardingEventConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationInvitationConfiguration());
        modelBuilder.ApplyConfiguration(new BillingWebhookEventConfiguration());
        modelBuilder.ApplyConfiguration(new TenantInvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new TenantApiKeyConfiguration());
        SeedData.Apply(modelBuilder);
    }

    private void UpdateAuditFields()
    {
        var utcNow = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(entity => entity.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = utcNow;
            }
        }
    }

    private void ValidateTenantOwnership()
    {
        var changedEntries = ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToList();

        var createdCompanyIds = changedEntries
            .Where(entry => entry.State == EntityState.Added && entry.Entity is Company)
            .Select(entry => ((Company)entry.Entity).Id)
            .ToHashSet();

        foreach (var entry in changedEntries)
        {
            if (entry.Entity is SubscriptionPlan or PlanEntitlement)
            {
                continue;
            }

            var companyProperty = entry.Properties.FirstOrDefault(property => string.Equals(property.Metadata.Name, nameof(User.CompanyId), StringComparison.Ordinal));
            if (companyProperty is null)
            {
                continue;
            }

            if (companyProperty.CurrentValue is not Guid companyId || companyId == Guid.Empty)
            {
                throw new DomainValidationException("Tenant-owned records must include a valid CompanyId.", "tenant_company_required");
            }

            var isUserEntry = entry.Metadata.ClrType == typeof(User);

            if (_tenantContext?.IsAuthenticated == true && _tenantContext.CompanyId is { } tenantCompanyId && tenantCompanyId != Guid.Empty)
            {
                var isRecordForNewlyCreatedCompany = entry.State == EntityState.Added && createdCompanyIds.Contains(companyId);
                if (!isUserEntry && companyId != tenantCompanyId && !isRecordForNewlyCreatedCompany)
                {
                    throw new DomainValidationException("Cross-tenant write was blocked.", "tenant_write_mismatch");
                }

                if (!isUserEntry
                    && entry.State == EntityState.Modified
                    && entry.Properties.Any(property => string.Equals(property.Metadata.Name, nameof(User.CompanyId), StringComparison.Ordinal) && property.IsModified))
                {
                    throw new DomainValidationException("CompanyId updates are not allowed on tenant-owned records.", "tenant_company_immutable");
                }
            }

            ValidateTenantForeignKeys(entry, companyId);
        }
    }

    private void ValidateTenantForeignKeys(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, Guid companyId)
    {
        foreach (var foreignKey in entry.Metadata.GetForeignKeys())
        {
            if (foreignKey.PrincipalEntityType.ClrType == typeof(Company))
            {
                continue;
            }

            if (entry.Metadata.ClrType == typeof(OrganizationMember)
                && foreignKey.PrincipalEntityType.ClrType == typeof(User))
            {
                continue;
            }

            var principalCompanyProperty = foreignKey.PrincipalEntityType.FindProperty(nameof(User.CompanyId));
            if (principalCompanyProperty is null)
            {
                continue;
            }

            var foreignKeyValues = foreignKey.Properties
                .Select(property => entry.Property(property.Name).CurrentValue)
                .ToArray();

            if (foreignKeyValues.Any(value => value is null))
            {
                continue;
            }

            var principalEntity = Find(foreignKey.PrincipalEntityType.ClrType, foreignKeyValues!);
            if (principalEntity is null)
            {
                continue;
            }

            var principalCompanyValue = principalEntity.GetType().GetProperty(nameof(User.CompanyId))?.GetValue(principalEntity);
            if (principalCompanyValue is Guid principalCompanyId && principalCompanyId != companyId)
            {
                throw new DomainValidationException("Cross-tenant foreign key relationship was blocked.", "tenant_foreign_key_mismatch");
            }
        }
    }
}
