namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class ConciergeIntelligenceOptions
{
    public const string SectionName = "ConciergeIntelligence";

    public int RecentMessageCount { get; init; } = 10;
    public int MemoryCharacterBudget { get; init; } = 3000;
    public int MaximumIntents { get; init; } = 3;
    public int MaximumCandidates { get; init; } = 8;
    public int MaximumSelectedItems { get; init; } = 3;
    public int ContextCharacterBudget { get; init; } = 10000;
    public double MinimumLexicalScore { get; init; } = 0.35;
    public double MinimumSemanticScore { get; init; } = 0.18;
    public double MinimumFinalScore { get; init; } = 0.32;
    public double HighConfidenceThreshold { get; init; } = 0.72;
    public double MediumConfidenceThreshold { get; init; } = 0.46;
    public double MinimumScoreGap { get; init; } = 0.10;
    public double IntentWeight { get; init; } = 0.34;
    public double LexicalWeight { get; init; } = 0.40;
    public double SemanticWeight { get; init; } = 0.20;
    public double PriorityWeight { get; init; } = 0.06;
    public double EmergencyMismatchPenalty { get; init; } = 0.45;
    public double UnrelatedCategoryPenalty { get; init; } = 0.20;
    public bool EnableSemanticScoring { get; init; } = true;
    public bool EnableTypoTolerance { get; init; } = true;
    public int MaximumFuzzyExpansions { get; init; } = 6;
    public string DefaultTone { get; init; } = "Warm";
}
