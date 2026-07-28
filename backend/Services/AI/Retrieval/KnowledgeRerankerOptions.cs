namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class KnowledgeRerankerOptions
{
    public const string SectionName = "KnowledgeReranker";

    public bool Enabled { get; init; } = true;
    public double PriorSelectionBoost { get; init; } = 0.03;
    public double ClarificationTopicBoost { get; init; } = 0.02;
}