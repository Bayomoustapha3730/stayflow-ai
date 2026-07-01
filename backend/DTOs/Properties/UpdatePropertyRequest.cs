namespace StayFlow.Api.DTOs.Properties;

public sealed class UpdatePropertyRequest
{
    public string Name { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string CountryCode { get; init; } = "KE";
    public string TimeZone { get; init; } = "Africa/Nairobi";
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyCollection<AmenityRequest> Amenities { get; init; } = [];
    public IReadOnlyCollection<HouseRuleRequest> HouseRules { get; init; } = [];
    public IReadOnlyCollection<LocalRecommendationRequest> LocalRecommendations { get; init; } = [];
    public IReadOnlyCollection<EmergencyContactRequest> EmergencyContacts { get; init; } = [];
    public IReadOnlyCollection<PropertyKnowledgeBaseItemRequest> KnowledgeBaseItems { get; init; } = [];
}
