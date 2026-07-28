namespace StayFlow.Api.Services.AI.Context;

public enum ContextConfidenceLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public sealed record ContextConfidenceResult(
    int Score,
    ContextConfidenceLevel Level,
    IReadOnlyCollection<string> Reasons,
    IReadOnlyCollection<ConversationContextWarning> MissingContext);
