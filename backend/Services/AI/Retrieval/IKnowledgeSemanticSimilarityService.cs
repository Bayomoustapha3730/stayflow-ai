using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Retrieval;

public interface IKnowledgeSemanticSimilarityService
{
    double Score(
        ConversationContextKnowledgeItem item,
        ConversationIntentResult intentResult,
        KnowledgeQueryExpansionResult expansion);
}
