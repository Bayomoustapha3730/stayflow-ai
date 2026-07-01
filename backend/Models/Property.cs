namespace StayFlow.Api.Models;

public sealed class Property : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public ICollection<Amenity> Amenities { get; set; } = [];
    public ICollection<HouseRule> HouseRules { get; set; } = [];
    public ICollection<LocalRecommendation> LocalRecommendations { get; set; } = [];
    public ICollection<EmergencyContact> EmergencyContacts { get; set; } = [];
    public ICollection<KnowledgeBaseItem> KnowledgeBaseItems { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
