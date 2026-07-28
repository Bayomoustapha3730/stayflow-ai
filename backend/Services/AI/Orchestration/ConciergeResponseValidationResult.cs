namespace StayFlow.Api.Services.AI.Orchestration;

public sealed record ConciergeResponseValidationResult(
    bool IsValid,
    string Outcome,
    IReadOnlyCollection<string> ViolationCodes,
    IReadOnlyCollection<string> AllowedSourceArticleIds,
    IReadOnlyCollection<string> ReferencedSourceArticleIds);