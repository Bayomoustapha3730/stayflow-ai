namespace StayFlow.Api.Services.AI.Retrieval;

public sealed record KnowledgeConfidenceResult(
    double Score,
    KnowledgeConfidenceLevel Level,
    double TopScore,
    double SecondScore,
    double ScoreGap,
    double IntentConfidence,
    double Coverage,
    bool IsAmbiguous,
    KnowledgeRetrievalReasonCode ReasonCode);
