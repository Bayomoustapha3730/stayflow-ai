using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Memory;

public interface IConversationSummaryService
{
    string BuildSummary(
        IReadOnlyCollection<string> recentGuestMessages,
        IReadOnlyCollection<string> recentAssistantMessages,
        GuestIntent? lastIntent,
        PendingClarificationContext? pendingClarificationContext);
}