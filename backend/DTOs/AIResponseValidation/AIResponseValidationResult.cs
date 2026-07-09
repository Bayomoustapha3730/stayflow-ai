namespace StayFlow.Api.DTOs.AIResponseValidation;

public sealed class AIResponseValidationResult
{
    public AIResponseValidationOutcome Outcome { get; init; }
    public IReadOnlyCollection<AIResponseViolationCode> Violations { get; init; } = [];
    public string? SafeMessage { get; init; }
}
