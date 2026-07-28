using StayFlow.Api.Services.AI.Retrieval;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed record ConciergeResponseResult(
    string Text,
    ConciergeResponseOutcome Outcome,
    bool RequiresEscalation,
    bool RequiresClarification,
    IReadOnlyCollection<string> SourceArticleIds,
    KnowledgeConfidenceLevel ConfidenceLevel,
    string SafetyClassification);
