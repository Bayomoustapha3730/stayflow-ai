using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Services.AI.Safety;

public interface IAIReplySafetyEvaluator
{
    AIReplySafetyResult Evaluate(
        AIReplyOperation operation,
        string? output,
        IReadOnlyCollection<string> suggestions,
        ConversationContext context,
        int contextConfidence,
        bool fallbackUsed);
}
