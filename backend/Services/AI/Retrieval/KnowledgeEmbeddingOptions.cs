namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class KnowledgeEmbeddingOptions
{
    public const string SectionName = "KnowledgeEmbedding";

    public bool EnableEmbeddingBlend { get; init; } = false;
    public double EmbeddingWeight { get; init; } = 0.20;
}