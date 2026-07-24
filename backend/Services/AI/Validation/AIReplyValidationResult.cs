using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Services.AI.Validation;

public sealed record AIReplyValidationResult(
    bool IsValid,
    string? NormalizedOutput,
    IReadOnlyCollection<string> NormalizedSuggestions,
    IReadOnlyCollection<string> Errors,
    IReadOnlyCollection<AIReplyOrchestrationWarning> Warnings);
