namespace StayFlow.Api.Services.AI.Retrieval;

public interface IKnowledgeEmbeddingProvider
{
    KnowledgeEmbeddingResult CreateEmbedding(string text);
}

public sealed record KnowledgeEmbeddingResult(
    bool Success,
    IReadOnlyCollection<double> Vector,
    string Provider,
    string? FailureReason);