using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.DTOs.AIPrompt;

public sealed class AIReplyPromptBuildRequest
{
    public ConversationContext ConversationContext { get; init; } = null!;
    public GuestIntentResult Intent { get; init; } = null!;
    public IReadOnlyCollection<ConversationContextKnowledgeItem> SelectedKnowledgeItems { get; init; } = [];
    public AIReplyOperation Operation { get; init; }
    public string? RequestedTone { get; init; }
    public string? HostInstruction { get; init; }
    public string? HostDraft { get; init; }
    public int MaxResponseCharacters { get; init; } = 1500;
}
