namespace StayFlow.Api.Models;

public sealed class Company : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<Guest> Guests { get; set; } = [];
    public ICollection<Reservation> Reservations { get; set; } = [];
    public ICollection<KnowledgeBaseItem> KnowledgeBaseItems { get; set; } = [];
    public ICollection<PropertyKnowledgeArticle> PropertyKnowledgeArticles { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<ConversationMessage> ConversationMessages { get; set; } = [];
    public ICollection<ServiceProvider> ServiceProviders { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
