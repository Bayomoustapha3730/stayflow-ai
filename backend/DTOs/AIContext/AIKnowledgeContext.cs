namespace StayFlow.Api.DTOs.AIContext;

public sealed class AIKnowledgeContext
{
    public IReadOnlyCollection<AIKnowledgeArticleContext> Articles { get; init; } = [];
    public IReadOnlyCollection<AIAmenityContext> Amenities { get; init; } = [];
    public IReadOnlyCollection<AIHouseRuleContext> HouseRules { get; init; } = [];
    public IReadOnlyCollection<AIRecommendationContext> Recommendations { get; init; } = [];
    public IReadOnlyCollection<AIEmergencyContactContext> EmergencyContacts { get; init; } = [];
    public string KnowledgeSensitivityLimitation { get; init; } = "Property knowledge articles do not yet support sensitivity classification; access-like content is excluded by deterministic keyword safety filters.";
}

public sealed class AIKnowledgeArticleContext
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public sealed class AIAmenityContext
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class AIHouseRuleContext
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class AIRecommendationContext
{
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
}

public sealed class AIEmergencyContactContext
{
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
}
