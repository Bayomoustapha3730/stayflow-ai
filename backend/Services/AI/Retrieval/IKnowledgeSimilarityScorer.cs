using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Retrieval;

public interface IKnowledgeSimilarityScorer
{
    double Score(
        ConversationContextKnowledgeItem item,
        GuestIntentResult intent,
        string normalizedQuery,
        IReadOnlyCollection<string> queryTokens);
}
