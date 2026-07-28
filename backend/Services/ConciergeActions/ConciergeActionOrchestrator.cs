using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Memory;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ConciergeActionOrchestrator(
    ApplicationDbContext dbContext,
    IConciergeActionDetector detector,
    IConciergeActionPolicy policy,
    IConciergeActionExecutor executor,
    IConciergeActionAuditService auditService,
    IConciergeActionConfirmationService confirmationService,
    IConciergeActionIdempotencyService idempotencyService,
    IConciergeActionResultFormatter formatter,
    IConversationMemoryService memoryService,
    IOptions<ConciergeActionsOptions> options,
    ICurrentTenantContext tenantContext) : IConciergeActionOrchestrator
{
    public async Task<ConciergeActionOrchestrationResult> HandleGuestMessageAsync(
        Guid companyId,
        Conversation conversation,
        Guid guestMessageId,
        string guestMessage,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || conversation.PropertyId is null)
        {
            return NotHandled();
        }

        var pending = await GetCurrentPendingActionAsync(companyId, conversation.Id, cancellationToken);
        if (pending is not null)
        {
            var maybeExisting = await HandlePendingConversationReplyAsync(conversation, pending, guestMessage, cancellationToken);
            if (maybeExisting.Handled)
            {
                return maybeExisting;
            }
        }

        if (await IsRateLimitedAsync(companyId, conversation.Id, cancellationToken))
        {
            return new ConciergeActionOrchestrationResult(true, "I couldn't submit that request right now. Nothing was changed. Please try again or contact the host.", null, null, false, null, "RateLimited");
        }

        var memory = memoryService.BuildContext(conversation.ToContext(), 5, 1500);
        var proposal = detector.Detect(conversation, guestMessage, memory.ActiveTopic, pending is not null);

        if (proposal.ActionType == ConciergeActionType.None || proposal.ConfidenceLevel != ConciergeActionConfidenceLevel.High)
        {
            if (proposal.RequiresClarification && !string.IsNullOrWhiteSpace(proposal.ClarificationPrompt))
            {
                return new ConciergeActionOrchestrationResult(true, proposal.ClarificationPrompt!, null, null, false, PendingConciergeActionStatus.AwaitingGuestConfirmation);
            }

            return NotHandled();
        }

        await auditService.WriteAsync(
            companyId,
            conversation.Id,
            null,
            proposal.ActionType,
            ConciergeActionAuditEventType.Detected,
            "Guest",
            null,
            conversation.Channel.ToString(),
            proposal.ReasonCode,
            tenantContext.CorrelationId ?? "none",
            new { proposal.ConfidenceLevel, proposal.IsExplicitRequest },
            cancellationToken);

        var policyResult = policy.Validate(conversation, proposal);
        if (!policyResult.Allowed)
        {
            await auditService.WriteAsync(
                companyId,
                conversation.Id,
                null,
                proposal.ActionType,
                ConciergeActionAuditEventType.PolicyRejected,
                "System",
                tenantContext.UserId,
                conversation.Channel.ToString(),
                policyResult.FailureCode ?? "PolicyRejected",
                tenantContext.CorrelationId ?? "none",
                null,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(policyResult.ClarificationPrompt))
            {
                return new ConciergeActionOrchestrationResult(true, policyResult.ClarificationPrompt!, null, null, false, null, policyResult.FailureCode);
            }

            return NotHandled();
        }

        var serialized = ConciergeActionSerialization.Serialize(proposal.ParsedParameters!);
        var key = idempotencyService.CreateKey(companyId, conversation.Id, proposal.ActionType, conversation.PropertyId.Value, conversation.ReservationId, serialized);
        var existing = await dbContext.PendingConciergeActions
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.IdempotencyKey == key, cancellationToken);

        if (existing is not null)
        {
            if (existing.Status == PendingConciergeActionStatus.Completed || existing.Status == PendingConciergeActionStatus.AwaitingHostApproval)
            {
                var replayResult = new ConciergeActionExecutionResult(existing.Id, existing.ActionType, existing.Status, false, true, null, existing.Status == PendingConciergeActionStatus.AwaitingHostApproval, false, ConciergeActionResponseCodes.AlreadySubmitted, null, existing.ExecutedAt);
                return new ConciergeActionOrchestrationResult(true, formatter.ToGuestMessage(replayResult), null, replayResult, existing.Status == PendingConciergeActionStatus.AwaitingHostApproval, existing.Status);
            }

            if (existing.Status == PendingConciergeActionStatus.AwaitingGuestConfirmation)
            {
                var prompt = BuildConfirmationPrompt(existing.ActionType, proposal.ParsedParameters!, policyResult.RequiresHostApproval);
                return new ConciergeActionOrchestrationResult(true, prompt, ToPendingCard(existing, policyResult.ConfirmationRequirement, prompt, policyResult.RequiresHostApproval), null, false, existing.Status);
            }
        }

        var requiresGuestConfirmation = policyResult.ConfirmationRequirement is ConciergeActionConfirmationRequirement.ExplicitGuestConfirmation or ConciergeActionConfirmationRequirement.Both;
        var status = requiresGuestConfirmation && !confirmationService.IsAffirmative(guestMessage)
            ? PendingConciergeActionStatus.AwaitingGuestConfirmation
            : PendingConciergeActionStatus.ReadyToExecute;

        var pendingAction = new PendingConciergeAction
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConversationId = conversation.Id,
            PropertyId = conversation.PropertyId.Value,
            ReservationId = conversation.ReservationId,
            ActionType = proposal.ActionType,
            SerializedNormalizedParameters = serialized,
            Status = status,
            IdempotencyKey = key,
            CreatedFromMessageId = guestMessageId,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(options.Value.PendingActionExpirationMinutes)
        };

        await dbContext.PendingConciergeActions.AddAsync(pendingAction, cancellationToken);

        if (status == PendingConciergeActionStatus.AwaitingGuestConfirmation)
        {
            var prompt = BuildConfirmationPrompt(proposal.ActionType, proposal.ParsedParameters!, policyResult.RequiresHostApproval);
            await auditService.WriteAsync(
                companyId,
                conversation.Id,
                pendingAction.Id,
                proposal.ActionType,
                ConciergeActionAuditEventType.ConfirmationRequested,
                "System",
                tenantContext.UserId,
                conversation.Channel.ToString(),
                "AwaitingGuestConfirmation",
                tenantContext.CorrelationId ?? "none",
                null,
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            return new ConciergeActionOrchestrationResult(true, prompt, ToPendingCard(pendingAction, policyResult.ConfirmationRequirement, prompt, policyResult.RequiresHostApproval), null, false, pendingAction.Status);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var result = await executor.ExecuteAsync(pendingAction, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ConciergeActionOrchestrationResult(true, formatter.ToGuestMessage(result), null, result, result.RequiresHostApproval, result.Status);
    }

    public async Task<ConciergeActionOrchestrationResult> ConfirmPendingActionAsync(
        Guid companyId,
        Guid conversationId,
        Guid actionId,
        CancellationToken cancellationToken)
    {
        var pending = await dbContext.PendingConciergeActions
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.ConversationId == conversationId && item.Id == actionId, cancellationToken);

        if (pending is null)
        {
            return NotHandled();
        }

        if (pending.Status == PendingConciergeActionStatus.Completed || pending.Status == PendingConciergeActionStatus.AwaitingHostApproval)
        {
            var replay = new ConciergeActionExecutionResult(pending.Id, pending.ActionType, pending.Status, false, true, null, pending.Status == PendingConciergeActionStatus.AwaitingHostApproval, false, ConciergeActionResponseCodes.AlreadySubmitted, null, pending.ExecutedAt);
            return new ConciergeActionOrchestrationResult(true, formatter.ToGuestMessage(replay), null, replay, pending.Status == PendingConciergeActionStatus.AwaitingHostApproval, pending.Status);
        }

        if (pending.Status != PendingConciergeActionStatus.AwaitingGuestConfirmation)
        {
            return new ConciergeActionOrchestrationResult(true, "That request is no longer awaiting confirmation.", null, null, false, pending.Status, "InvalidStatus");
        }

        if (pending.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            pending.Status = PendingConciergeActionStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ConciergeActionOrchestrationResult(true, "That request has expired. Please send a new request.", null, null, false, pending.Status, "Expired");
        }

        pending.Status = PendingConciergeActionStatus.ReadyToExecute;
        pending.ConfirmedAt = DateTimeOffset.UtcNow;
        await auditService.WriteAsync(
            companyId,
            conversationId,
            pending.Id,
            pending.ActionType,
            ConciergeActionAuditEventType.Confirmed,
            "Guest",
            null,
            "Chat",
            "Confirmed",
            tenantContext.CorrelationId ?? "none",
            null,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        var result = await executor.ExecuteAsync(pending, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ConciergeActionOrchestrationResult(true, formatter.ToGuestMessage(result), null, result, result.RequiresHostApproval, result.Status);
    }

    public async Task<ConciergeActionOrchestrationResult> CancelPendingActionAsync(
        Guid companyId,
        Guid conversationId,
        Guid actionId,
        CancellationToken cancellationToken)
    {
        var pending = await dbContext.PendingConciergeActions
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.ConversationId == conversationId && item.Id == actionId, cancellationToken);

        if (pending is null)
        {
            return NotHandled();
        }

        if (pending.Status is PendingConciergeActionStatus.Completed or PendingConciergeActionStatus.AwaitingHostApproval)
        {
            return new ConciergeActionOrchestrationResult(true, "That request was already submitted.", null, null, true, pending.Status);
        }

        pending.Status = PendingConciergeActionStatus.Cancelled;
        pending.CancelledAt = DateTimeOffset.UtcNow;

        await auditService.WriteAsync(
            companyId,
            conversationId,
            pending.Id,
            pending.ActionType,
            ConciergeActionAuditEventType.Cancelled,
            "Guest",
            null,
            "Chat",
            "Cancelled",
            tenantContext.CorrelationId ?? "none",
            null,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ConciergeActionOrchestrationResult(true, "Okay, I cancelled that request.", null, null, false, pending.Status);
    }

    private async Task<ConciergeActionOrchestrationResult> HandlePendingConversationReplyAsync(
        Conversation conversation,
        PendingConciergeAction pending,
        string guestMessage,
        CancellationToken cancellationToken)
    {
        if (pending.Status != PendingConciergeActionStatus.AwaitingGuestConfirmation)
        {
            return NotHandled();
        }

        if (pending.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            pending.Status = PendingConciergeActionStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ConciergeActionOrchestrationResult(true, "That request has expired. Please send a new request.", null, null, false, pending.Status, "Expired");
        }

        if (confirmationService.IsCancel(guestMessage) || confirmationService.IsNegative(guestMessage))
        {
            pending.Status = PendingConciergeActionStatus.Cancelled;
            pending.CancelledAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ConciergeActionOrchestrationResult(true, "Okay, I cancelled that request.", null, null, false, pending.Status);
        }

        if (confirmationService.IsAffirmative(guestMessage))
        {
            return await ConfirmPendingActionAsync(conversation.CompanyId, conversation.Id, pending.Id, cancellationToken);
        }

        if (TryUpdatePendingTime(pending, guestMessage))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var prompt = "I updated that request. Should I submit it now?";
            return new ConciergeActionOrchestrationResult(true, prompt, ToPendingCard(pending, ConciergeActionConfirmationRequirement.ExplicitGuestConfirmation, prompt, pending.ActionType is ConciergeActionType.RequestEarlyCheckIn or ConciergeActionType.RequestLateCheckout or ConciergeActionType.RequestParking), null, false, pending.Status);
        }

        return NotHandled();
    }

    private static bool TryUpdatePendingTime(PendingConciergeAction pending, string guestMessage)
    {
        var normalized = guestMessage.Trim().ToLowerInvariant();
        if (pending.ActionType is not (ConciergeActionType.RequestEarlyCheckIn or ConciergeActionType.RequestLateCheckout))
        {
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(normalized, @"\b\d{1,2}(:\d{2})?\s?(am|pm)?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success || !TimeOnly.TryParse(match.Value, out var parsed))
        {
            return false;
        }

        if (pending.ActionType == ConciergeActionType.RequestEarlyCheckIn)
        {
            var payload = ConciergeActionSerialization.Deserialize(pending.ActionType, pending.SerializedNormalizedParameters);
            var current = (EarlyCheckInRequestAction)payload;
            pending.SerializedNormalizedParameters = ConciergeActionSerialization.Serialize(current with { RequestedTime = parsed });
            return true;
        }

        var latePayload = ConciergeActionSerialization.Deserialize(pending.ActionType, pending.SerializedNormalizedParameters);
        var late = (LateCheckoutRequestAction)latePayload;
        pending.SerializedNormalizedParameters = ConciergeActionSerialization.Serialize(late with { RequestedTime = parsed });
        return true;
    }

    private async Task<PendingConciergeAction?> GetCurrentPendingActionAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        return await dbContext.PendingConciergeActions
            .Where(item => item.CompanyId == companyId
                && item.ConversationId == conversationId
                && (item.Status == PendingConciergeActionStatus.AwaitingGuestConfirmation || item.Status == PendingConciergeActionStatus.ReadyToExecute || item.Status == PendingConciergeActionStatus.Executing))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsRateLimitedAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        var proposalWindow = DateTimeOffset.UtcNow.AddMinutes(-10);
        var executionWindow = DateTimeOffset.UtcNow.AddHours(-1);

        var proposals = await dbContext.PendingConciergeActions.CountAsync(item =>
            item.CompanyId == companyId
            && item.ConversationId == conversationId
            && item.CreatedAt >= proposalWindow,
            cancellationToken);

        if (proposals >= 10)
        {
            return true;
        }

        var executions = await dbContext.PendingConciergeActions.CountAsync(item =>
            item.CompanyId == companyId
            && item.ConversationId == conversationId
            && item.ExecutedAt != null
            && item.ExecutedAt >= executionWindow,
            cancellationToken);

        return executions >= options.Value.MaximumActionsPerConversationPerHour;
    }

    private static PendingActionCardDto ToPendingCard(
        PendingConciergeAction pending,
        ConciergeActionConfirmationRequirement requirement,
        string prompt,
        bool requiresHostApproval)
    {
        return new PendingActionCardDto(pending.Id, pending.ActionType, pending.Status, requirement, prompt, requiresHostApproval, pending.ExpiresAt);
    }

    private static string BuildConfirmationPrompt(ConciergeActionType actionType, object parameters, bool requiresHostApproval)
    {
        return actionType switch
        {
            ConciergeActionType.RequestEarlyCheckIn => $"I can submit an early check-in request{FormatTime(parameters)}. {(requiresHostApproval ? "This requires host approval. " : string.Empty)}Should I submit it?",
            ConciergeActionType.RequestLateCheckout => $"I can submit a late checkout request{FormatTime(parameters)}. {(requiresHostApproval ? "This requires host approval. " : string.Empty)}Should I submit it?",
            ConciergeActionType.RequestParking => "I can submit a parking request. This requires host approval. Should I submit it?",
            ConciergeActionType.NotifyHost => "I can notify the host. Should I submit it?",
            _ => "Should I submit this request?"
        };
    }

    private static string FormatTime(object parameters)
    {
        return parameters switch
        {
            EarlyCheckInRequestAction early when early.RequestedTime.HasValue => $" for {early.RequestedTime.Value:HH\\:mm}",
            LateCheckoutRequestAction late when late.RequestedTime.HasValue => $" for {late.RequestedTime.Value:HH\\:mm}",
            _ => string.Empty
        };
    }

    private static ConciergeActionOrchestrationResult NotHandled()
        => new(false, string.Empty, null, null, false, null);
}

internal static class ConversationMemoryExtensions
{
    public static Services.AI.Context.ConversationContext ToContext(this Conversation conversation)
    {
        return new Services.AI.Context.ConversationContext(
            conversation.Id,
            conversation.CompanyId,
            conversation.Status.ToString(),
            conversation.Channel.ToString(),
            conversation.Subject ?? string.Empty,
            conversation.HumanTakeoverEnabled || conversation.Status is ConversationStatus.AwaitingHost or ConversationStatus.Escalated or ConversationStatus.HumanManaged,
            conversation.HumanTakeoverEnabled,
            conversation.AssignedUser?.FullName,
            conversation.Guest?.FirstName ?? "Guest",
            conversation.Guest?.Email,
            conversation.PropertyId,
            conversation.Property?.Name,
            conversation.ReservationId,
            conversation.Reservation?.ConfirmationNumber,
            conversation.Reservation?.CheckInDate,
            conversation.Reservation?.CheckOutDate,
            [],
            [],
            [],
            [],
            false,
            DateTimeOffset.UtcNow);
    }
}
