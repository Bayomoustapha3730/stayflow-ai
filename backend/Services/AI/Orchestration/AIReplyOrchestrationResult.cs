using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class AIReplyOrchestrationResult
{
    public Guid ConversationId { get; init; }
    public AIReplyOperation Operation { get; init; }
    public string? Output { get; init; }
    public IReadOnlyCollection<string> Suggestions { get; init; } = [];
    public int ContextMessageCount { get; init; }
    public int Confidence { get; init; }
    public IReadOnlyCollection<ConversationContextSource> Sources { get; init; } = [];
    public IReadOnlyCollection<AIReplyOrchestrationWarning> Warnings { get; init; } = [];
    public GuestIntentResult? DetectedIntent { get; init; }
    public string Provider { get; init; } = string.Empty;
    public bool IsMock { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public bool ContextTruncated { get; init; }
    public bool FallbackUsed { get; init; }
    public IReadOnlyCollection<AIReplyOrchestrationStage> CompletedStages { get; init; } = [];
    public long DurationMilliseconds { get; init; }
    public bool RequiresHumanReview { get; init; }
}
