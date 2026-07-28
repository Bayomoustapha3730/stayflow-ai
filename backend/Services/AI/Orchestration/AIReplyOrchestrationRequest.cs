namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class AIReplyOrchestrationRequest
{
    public Guid ConversationId { get; init; }
    public AIReplyOperation Operation { get; init; }
    public string? RequestedTone { get; init; }
    public string? HostDraft { get; init; }
    public string? HostInstruction { get; init; }
    public int? RequestedSuggestionCount { get; init; }
    public string? CorrelationId { get; init; }
}
