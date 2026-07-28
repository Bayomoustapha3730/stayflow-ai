using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.ConciergeActions;

public interface IConciergeActionDetector
{
    ConciergeActionProposal Detect(
        Conversation conversation,
        string guestMessage,
        string? activeTopic,
        bool hasPendingAction);
}

public interface IConciergeActionPolicy
{
    (bool Allowed, string? FailureCode, string? ClarificationPrompt, ConciergeActionConfirmationRequirement ConfirmationRequirement, bool RequiresHostApproval) Validate(
        Conversation conversation,
        ConciergeActionProposal proposal);
}

public interface IConciergeActionIdempotencyService
{
    string CreateKey(Guid companyId, Guid conversationId, ConciergeActionType actionType, Guid propertyId, Guid? reservationId, string normalizedParameters);
}

public interface IConciergeActionAuditService
{
    Task WriteAsync(
        Guid companyId,
        Guid conversationId,
        Guid? pendingActionId,
        ConciergeActionType actionType,
        ConciergeActionAuditEventType eventType,
        string actorType,
        Guid? actorUserId,
        string channel,
        string resultCode,
        string correlationId,
        object? metadata,
        CancellationToken cancellationToken);
}

public interface IConciergeActionResultFormatter
{
    string ToGuestMessage(ConciergeActionExecutionResult result);
}

public interface IConciergeActionConfirmationService
{
    bool IsAffirmative(string message);
    bool IsNegative(string message);
    bool IsCancel(string message);
}

public interface IConciergeActionExecutor
{
    Task<ConciergeActionExecutionResult> ExecuteAsync(PendingConciergeAction pendingAction, CancellationToken cancellationToken);
}

public interface IConciergeActionHandler<TAction>
{
    ConciergeActionType ActionType { get; }
    Task<ConciergeActionExecutionResult> HandleAsync(Guid pendingActionId, TAction action, CancellationToken cancellationToken);
}

public interface IConciergeActionOrchestrator
{
    Task<ConciergeActionOrchestrationResult> HandleGuestMessageAsync(
        Guid companyId,
        Conversation conversation,
        Guid guestMessageId,
        string guestMessage,
        CancellationToken cancellationToken);

    Task<ConciergeActionOrchestrationResult> ConfirmPendingActionAsync(
        Guid companyId,
        Guid conversationId,
        Guid actionId,
        CancellationToken cancellationToken);

    Task<ConciergeActionOrchestrationResult> CancelPendingActionAsync(
        Guid companyId,
        Guid conversationId,
        Guid actionId,
        CancellationToken cancellationToken);
}

public sealed record ConciergeActionOrchestrationResult(
    bool Handled,
    string AssistantMessage,
    PendingActionCardDto? PendingAction,
    ConciergeActionExecutionResult? ExecutionResult,
    bool RequiresHostAttention,
    PendingConciergeActionStatus? ConversationActionStatus,
    string? FailureCode = null);
