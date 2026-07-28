namespace StayFlow.Api.DTOs.AIOrchestration;

public sealed class AIKnowledgeSourceReference
{
    public Guid PropertyKnowledgeArticleId { get; init; }
    public int Rank { get; init; }
    public bool IsPrimary { get; init; }
    public string? RelevanceReason { get; init; }
}