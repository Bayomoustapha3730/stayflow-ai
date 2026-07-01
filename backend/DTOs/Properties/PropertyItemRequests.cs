namespace StayFlow.Api.DTOs.Properties;

public sealed class AmenityRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class HouseRuleRequest
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class LocalRecommendationRequest
{
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
}

public sealed class EmergencyContactRequest
{
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
}

public sealed class PropertyKnowledgeBaseItemRequest
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}
