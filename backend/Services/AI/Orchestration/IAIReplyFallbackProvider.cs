using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Orchestration;

public interface IAIReplyFallbackProvider
{
    string BuildFallback(
        AIReplyOperation operation,
        string? tone,
        GuestIntentResult? intent,
        bool includeReviewReminder);
}
