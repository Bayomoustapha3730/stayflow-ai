using StayFlow.Api.Models;

namespace StayFlow.Api.DTOs.ConciergeActions;

public sealed record ConciergeActionProposal(
    ConciergeActionType ActionType,
    ConciergeActionConfidenceLevel ConfidenceLevel,
    object? ParsedParameters,
    IReadOnlyCollection<string> MissingRequiredParameters,
    bool RequiresClarification,
    string? ClarificationPrompt,
    bool IsExplicitRequest,
    string ReasonCode);

public sealed record ConciergeActionExecutionResult(
    Guid ActionId,
    ConciergeActionType ActionType,
    PendingConciergeActionStatus Status,
    bool WasCreated,
    bool WasIdempotentReplay,
    Guid? DomainEntityId,
    bool RequiresHostApproval,
    bool HostNotificationCreated,
    string GuestSafeResultCode,
    string? FailureCode,
    DateTimeOffset? CompletedAt);

public sealed record PendingActionCardDto(
    Guid ActionId,
    ConciergeActionType ActionType,
    PendingConciergeActionStatus Status,
    ConciergeActionConfirmationRequirement ConfirmationRequirement,
    string Prompt,
    bool RequiresHostApproval,
    DateTimeOffset ExpiresAt);

public sealed record ConfirmPendingActionRequest
{
    public Guid GuestId { get; init; }
}

public sealed record CancelPendingActionRequest
{
    public Guid GuestId { get; init; }
}

public sealed record HostActionDecisionRequest
{
    public string? DecisionNote { get; init; }
}

public sealed record HostActionCompleteRequest
{
    public string? DecisionNote { get; init; }
}
