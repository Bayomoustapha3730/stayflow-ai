namespace StayFlow.Api.Services.AI.Context;

public sealed class ConversationContextLimits
{
    public const string SectionName = "ConversationContext";

    public int MaxVisibleMessages { get; init; } = 40;
    public int MaxMessageCharacters { get; init; } = 2000;
    public int MaxTotalPromptContextCharacters { get; init; } = 16000;
    public int MaxKnowledgeItems { get; init; } = 20;
    public int MaxKnowledgeItemCharacters { get; init; } = 4000;
    public int ContextScanPageSize { get; init; } = 100;
}
