namespace StayFlow.Api.Models;

public sealed class Property : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public ICollection<KnowledgeBaseItem> KnowledgeBaseItems { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
