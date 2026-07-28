using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Retrieval;

public interface IKnowledgeQueryExpander
{
    KnowledgeQueryExpansionResult Expand(string query, ConversationIntentResult intentResult);
}
