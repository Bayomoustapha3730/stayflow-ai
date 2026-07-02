namespace StayFlow.Api.DTOs.Properties;

public sealed class PropertyAmenityRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class PropertyHouseRuleRequest
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class PropertyRecommendationRequest
{
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
}

public sealed class PropertyEmergencyContactRequest
{
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
}

public sealed class PropertyKnowledgeArticleRequest
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}
