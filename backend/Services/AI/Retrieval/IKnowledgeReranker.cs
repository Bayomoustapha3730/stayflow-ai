using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Services.AI.Retrieval;

public interface IKnowledgeReranker
{
    IReadOnlyCollection<KnowledgeRetrievalCandidate> Rerank(
        IReadOnlyCollection<KnowledgeRetrievalCandidate> candidates,
        ConversationContext context,
        KnowledgeRetrievalRequest request,
        int maxCandidates);
}