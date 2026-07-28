using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Services.AI.Retrieval;

public interface IPropertyKnowledgeRetriever
{
    KnowledgeRetrievalResult Retrieve(ConversationContext context, KnowledgeRetrievalRequest request);
}
