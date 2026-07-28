using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Memory;

public sealed class DeterministicConversationSummaryService : IConversationSummaryService
{
    public string BuildSummary(
        IReadOnlyCollection<string> recentGuestMessages,
        IReadOnlyCollection<string> recentAssistantMessages,
        GuestIntent? lastIntent,
        PendingClarificationContext? pendingClarificationContext)
    {
        var parts = new List<string>
        {
            $"Recent guest turns: {recentGuestMessages.Count}",
            $"Recent assistant turns: {recentAssistantMessages.Count}",
            $"Last intent: {(lastIntent?.ToString() ?? "Unknown") }"
        };

        if (pendingClarificationContext is not null)
        {
            parts.Add($"Pending clarification: {pendingClarificationContext.Prompt}");
        }

        return string.Join("; ", parts);
    }
}