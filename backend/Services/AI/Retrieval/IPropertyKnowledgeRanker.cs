using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Retrieval;

public interface IPropertyKnowledgeRanker
{
    KnowledgeRetrievalResult Rank(
        ConversationContext context,
        GuestIntentResult intent,
        string latestGuestMessage,
        int maxSelectedItems,
        int maxSelectedCharacters);
}
