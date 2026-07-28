using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed record KnowledgeRetrievalCandidate(
    string ArticleId,
    PropertyKnowledgeCategory Category,
    double Score,
    double SemanticScore,
    IReadOnlyCollection<string> MatchSignals,
    int Rank,
    ConversationContextKnowledgeItem Item)
{
    public double LexicalScore { get; init; }
    public double IntentScore { get; init; }
    public double PriorityScore { get; init; }
    public double FinalScore { get; init; }
}

public sealed record KnowledgeRetrievalResult(
    GuestIntentResult QueryIntent,
    IReadOnlyCollection<KnowledgeRetrievalCandidate> Candidates,
    IReadOnlyCollection<KnowledgeRetrievalCandidate> SelectedItems,
    double Confidence,
    KnowledgeConfidenceLevel ConfidenceLevel,
    KnowledgeRetrievalReasonCode ReasonCode,
    bool WasCategoryRestricted,
    bool WasTruncated,
    bool RequiresClarification,
    bool EscalationRecommended,
    IReadOnlyCollection<string> ClarificationChoices,
    IReadOnlyCollection<string> Reasons)
{
    public ConversationIntentResult? IntentResult { get; init; }
    public bool IsAmbiguous { get; init; }
    public string? ClarificationPrompt { get; init; }
    public IReadOnlyDictionary<string, string> EvaluationMetadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
