namespace StayFlow.Api.Services.AI.Orchestration;

public sealed record ConciergeLanguageModelResult(
    string Output,
    bool Success,
    string Provider,
    string? Model,
    string? RequestId,
    int DurationMilliseconds,
    bool TimedOut,
    bool UsedFallback,
    IReadOnlyCollection<string> WarningCodes,
    string? FailureReason,
    string? ValidationOutcome,
    IReadOnlyCollection<string> SourceArticleIds,
    string? SafetyClassification,
    string? RawProviderResponse);