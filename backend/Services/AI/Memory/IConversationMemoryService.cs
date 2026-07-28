using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Services.AI.Memory;

public interface IConversationMemoryService
{
    ConversationMemoryContext BuildContext(
        ConversationContext context,
        int recentMessageCount,
        int characterBudget,
        IReadOnlyCollection<string>? priorSelectedArticleIds = null,
        string? pendingClarification = null);
}
