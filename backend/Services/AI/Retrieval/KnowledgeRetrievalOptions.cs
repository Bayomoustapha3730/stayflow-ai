namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class KnowledgeRetrievalOptions
{
    public const string SectionName = "KnowledgeRetrieval";

    public int MaxCandidates { get; init; } = 50;
    public int TopCandidateCount { get; init; } = 5;
    public int MaxSelectedItems { get; init; } = 3;
    public double MinimumScore { get; init; } = 18;
    public double MinimumConfidenceScore { get; init; } = 35;
    public double HighConfidenceScore { get; init; } = 68;
    public double MediumConfidenceScore { get; init; } = 42;
    public double MinimumScoreGap { get; init; } = 8;
    public double CategoryMatchWeight { get; init; } = 26;
    public double TitleMatchWeight { get; init; } = 24;
    public double TagMatchWeight { get; init; } = 14;
    public double SummaryMatchWeight { get; init; } = 12;
    public double ContentMatchWeight { get; init; } = 8;
    public double PriorityWeight { get; init; } = 1.2;
    public double EmergencyMismatchPenalty { get; init; } = 75;
    public int ContextCharacterBudget { get; init; } = 10000;
    public double SemanticSimilarityWeight { get; init; } = 12;
    public double UnrelatedCategoryPenalty { get; init; } = 8;
}
