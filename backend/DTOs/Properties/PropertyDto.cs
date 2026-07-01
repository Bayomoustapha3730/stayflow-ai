namespace StayFlow.Api.DTOs.Properties;

public sealed class PropertyDto
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string TimeZone { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyCollection<AmenityDto> Amenities { get; init; } = [];
    public IReadOnlyCollection<HouseRuleDto> HouseRules { get; init; } = [];
    public IReadOnlyCollection<LocalRecommendationDto> LocalRecommendations { get; init; } = [];
    public IReadOnlyCollection<EmergencyContactDto> EmergencyContacts { get; init; } = [];
    public IReadOnlyCollection<PropertyKnowledgeBaseItemDto> KnowledgeBaseItems { get; init; } = [];
}
