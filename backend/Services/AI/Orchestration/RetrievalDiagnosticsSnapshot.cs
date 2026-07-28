using StayFlow.Api.Services.AI.Retrieval;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class RetrievalDiagnosticsSnapshot
{
    public string DetectedIntent { get; init; } = string.Empty;
    public bool IntentAmbiguous { get; init; }
    public int IntentConfidenceScore { get; init; }
    public int SecondaryIntentCount { get; init; }
    public int CandidateCount { get; init; }
    public int SelectedCount { get; init; }
    public KnowledgeConfidenceLevel ConfidenceLevel { get; init; } = KnowledgeConfidenceLevel.None;
    public KnowledgeRetrievalReasonCode ReasonCode { get; init; } = KnowledgeRetrievalReasonCode.NoMatch;
    public bool ClarificationRequired { get; init; }
    public bool EscalationRecommended { get; init; }
    public IReadOnlyCollection<string> SelectedCategories { get; init; } = [];
    public IReadOnlyCollection<string> ClarificationChoices { get; init; } = [];
    public IReadOnlyCollection<string> WarningCodes { get; init; } = [];
    public IReadOnlyDictionary<string, string> EvaluationMetadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}