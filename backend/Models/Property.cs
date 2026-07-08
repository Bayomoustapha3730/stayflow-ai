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
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<PropertyAmenity> PropertyAmenities { get; set; } = [];
    public ICollection<PropertyHouseRule> PropertyHouseRules { get; set; } = [];
    public ICollection<PropertyRecommendation> PropertyRecommendations { get; set; } = [];
    public ICollection<PropertyEmergencyContact> PropertyEmergencyContacts { get; set; } = [];
    public ICollection<PropertyKnowledgeArticle> PropertyKnowledgeArticles { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
