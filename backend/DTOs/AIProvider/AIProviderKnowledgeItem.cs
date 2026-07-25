namespace StayFlow.Api.DTOs.AIProvider;

public sealed class AIProviderKnowledgeItem
{
    public string SourceId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Category { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public string? Summary { get; init; }
    public string Content { get; init; } = string.Empty;
    public int Priority { get; init; }
    public bool IsApproved { get; init; }
}