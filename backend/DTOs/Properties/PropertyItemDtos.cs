namespace StayFlow.Api.DTOs.Properties;

public sealed class PropertyAmenityDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class PropertyHouseRuleDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class PropertyRecommendationDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
}

public sealed class PropertyEmergencyContactDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
}

public sealed class PropertyKnowledgeArticleDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}
