namespace StayFlow.Api.DTOs.Properties;

public sealed class CreatePropertyRequest
{
    public string Name { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string CountryCode { get; init; } = "KE";
    public string TimeZone { get; init; } = "Africa/Nairobi";
    public string? Description { get; init; }
    public IReadOnlyCollection<PropertyAmenityRequest> PropertyAmenities { get; init; } = [];
    public IReadOnlyCollection<PropertyHouseRuleRequest> PropertyHouseRules { get; init; } = [];
    public IReadOnlyCollection<PropertyRecommendationRequest> PropertyRecommendations { get; init; } = [];
    public IReadOnlyCollection<PropertyEmergencyContactRequest> PropertyEmergencyContacts { get; init; } = [];
    public IReadOnlyCollection<PropertyKnowledgeArticleRequest> PropertyKnowledgeArticles { get; init; } = [];
}
