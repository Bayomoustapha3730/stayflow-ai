namespace StayFlow.Api.Services;

public sealed class AIContextOptions
{
    public const string SectionName = "AIContext";
    public int MaxKnowledgeArticles { get; init; } = 5;
    public int MaxRecommendations { get; init; } = 5;
    public int MaxHouseRules { get; init; } = 10;
    public int MaxEmergencyContacts { get; init; } = 5;
}
