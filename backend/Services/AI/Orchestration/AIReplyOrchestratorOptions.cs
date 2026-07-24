namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class AIReplyOrchestratorOptions
{
    public const string SectionName = "AI:Orchestrator";

    public int ProviderTimeoutSeconds { get; init; } = 15;
    public int MaximumSelectedKnowledgeItems { get; init; } = 5;
    public int MaximumSelectedKnowledgeCharacters { get; init; } = 10000;
    public bool EnableFallback { get; init; } = true;
}
