using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Services.AI.Safety;

public interface IAIReplySafetyEvaluator
{
    AIReplySafetyResult Evaluate(
        AIReplyOperation operation,
        string? output,
        IReadOnlyCollection<string> suggestions,
        ConversationContext context,
        IReadOnlyCollection<ConversationContextKnowledgeItem> selectedKnowledgeItems,
        GuestIntentResult? detectedIntent,
        int contextConfidence,
        bool fallbackUsed);
}
