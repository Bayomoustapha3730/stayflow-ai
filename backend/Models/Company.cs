namespace StayFlow.Api.Models;

public sealed class Company : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string NormalizedSlug { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public Guid? OwnerUserId { get; set; }
    public string? LegalName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string? BrandingLogoUrl { get; set; }
    public string? BrandingPrimaryColor { get; set; }
    public string? OnboardingState { get; set; }
    public string? StripeCustomerId { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<Guest> Guests { get; set; } = [];
    public ICollection<Reservation> Reservations { get; set; } = [];
    public ICollection<KnowledgeBaseItem> KnowledgeBaseItems { get; set; } = [];
    public ICollection<PropertyKnowledgeArticle> PropertyKnowledgeArticles { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<ConversationMessage> ConversationMessages { get; set; } = [];
    public ICollection<ConversationMessageKnowledgeSource> ConversationMessageKnowledgeSources { get; set; } = [];
    public ICollection<ConversationMessageFeedback> ConversationMessageFeedback { get; set; } = [];
    public ICollection<WhatsAppIntegration> WhatsAppIntegrations { get; set; } = [];
    public ICollection<WhatsAppTemplate> WhatsAppTemplates { get; set; } = [];
    public ICollection<OrganizationMember> OrganizationMembers { get; set; } = [];
    public ICollection<OrganizationInvitation> OrganizationInvitations { get; set; } = [];
    public ICollection<OnboardingProgress> OnboardingProgressRecords { get; set; } = [];
    public ICollection<TenantSubscription> TenantSubscriptions { get; set; } = [];
    public ICollection<TenantInvoice> TenantInvoices { get; set; } = [];
    public ICollection<TenantApiKey> TenantApiKeys { get; set; } = [];
    public ICollection<UsageRecord> UsageRecords { get; set; } = [];
    public ICollection<UsageOperation> UsageOperations { get; set; } = [];
    public ICollection<ServiceProvider> ServiceProviders { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
