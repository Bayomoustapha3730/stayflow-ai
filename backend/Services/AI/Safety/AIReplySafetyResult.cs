using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Services.AI.Safety;

public sealed record AIReplySafetyResult(
    bool Safe,
    IReadOnlyCollection<AIReplyOrchestrationWarning> Warnings,
    bool RequiresHumanReview,
    IReadOnlyCollection<string> BlockedReasons);
