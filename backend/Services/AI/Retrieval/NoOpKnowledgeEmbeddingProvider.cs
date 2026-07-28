namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class NoOpKnowledgeEmbeddingProvider : IKnowledgeEmbeddingProvider
{
    public KnowledgeEmbeddingResult CreateEmbedding(string text)
    {
        return new KnowledgeEmbeddingResult(
            false,
            [],
            "None",
            "Embedding provider is not configured.");
    }
}