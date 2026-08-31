using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ConciergeActionExecutor(
    ApplicationDbContext dbContext,
    IConciergeActionAuditService auditService,
    ICurrentTenantContext tenantContext,
    EarlyCheckInRequestHandler earlyCheckInRequestHandler,
    LateCheckoutRequestHandler lateCheckoutRequestHandler,
    MaintenanceTicketHandler maintenanceTicketHandler,
    HousekeepingRequestHandler housekeepingRequestHandler,
    ExtraItemRequestHandler extraItemRequestHandler,
    ParkingRequestHandler parkingRequestHandler,
    PaymentRequestHandler paymentRequestHandler,
    HostNotificationHandler hostNotificationHandler) : IConciergeActionExecutor
{
    public async Task<ConciergeActionExecutionResult> ExecuteAsync(PendingConciergeAction pendingAction, CancellationToken cancellationToken)
    {
        if (pendingAction.Status == PendingConciergeActionStatus.Completed)
        {
            await auditService.WriteAsync(
                pendingAction.CompanyId,
                pendingAction.ConversationId,
                pendingAction.Id,
                pendingAction.ActionType,
                ConciergeActionAuditEventType.IdempotentReplay,
                "System",
                tenantContext.UserId,
                "Chat",
                ConciergeActionResponseCodes.AlreadySubmitted,
                tenantContext.CorrelationId ?? "none",
                null,
                cancellationToken);

            return new ConciergeActionExecutionResult(
                pendingAction.Id,
                pendingAction.ActionType,
                PendingConciergeActionStatus.Completed,
                false,
                true,
                null,
                false,
                false,
                ConciergeActionResponseCodes.AlreadySubmitted,
                null,
                pendingAction.ExecutedAt);
        }

        if (pendingAction.Status != PendingConciergeActionStatus.ReadyToExecute)
        {
            return new ConciergeActionExecutionResult(
                pendingAction.Id,
                pendingAction.ActionType,
                pendingAction.Status,
                false,
                false,
                null,
                false,
                false,
                ConciergeActionResponseCodes.InvalidRequest,
                "InvalidStatus",
                null);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        pendingAction.Status = PendingConciergeActionStatus.Executing;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            pendingAction.CompanyId,
            pendingAction.ConversationId,
            pendingAction.Id,
            pendingAction.ActionType,
            ConciergeActionAuditEventType.ExecutionStarted,
            "System",
            tenantContext.UserId,
            "Chat",
            "ExecutionStarted",
            tenantContext.CorrelationId ?? "none",
            null,
            cancellationToken);

        ConciergeActionExecutionResult result;
        try
        {
            var payload = ConciergeActionSerialization.Deserialize(pendingAction.ActionType, pendingAction.SerializedNormalizedParameters);
            result = pendingAction.ActionType switch
            {
                ConciergeActionType.RequestEarlyCheckIn => await earlyCheckInRequestHandler.HandleAsync(pendingAction.Id, (EarlyCheckInRequestAction)payload, cancellationToken),
                ConciergeActionType.RequestLateCheckout => await lateCheckoutRequestHandler.HandleAsync(pendingAction.Id, (LateCheckoutRequestAction)payload, cancellationToken),
                ConciergeActionType.CreateMaintenanceTicket => await maintenanceTicketHandler.HandleAsync(pendingAction.Id, (MaintenanceTicketAction)payload, cancellationToken),
                ConciergeActionType.RequestHousekeeping => await housekeepingRequestHandler.HandleAsync(pendingAction.Id, (HousekeepingRequestAction)payload, cancellationToken),
                ConciergeActionType.RequestExtraItem => await extraItemRequestHandler.HandleAsync(pendingAction.Id, (ExtraItemRequestAction)payload, cancellationToken),
                ConciergeActionType.RequestParking => await parkingRequestHandler.HandleAsync(pendingAction.Id, (ParkingRequestAction)payload, cancellationToken),
                ConciergeActionType.RequestPayment => await paymentRequestHandler.HandleAsync(pendingAction.Id, (PaymentRequestAction)payload, cancellationToken),
                ConciergeActionType.NotifyHost => await hostNotificationHandler.HandleAsync(pendingAction.Id, (HostNotificationAction)payload, cancellationToken),
                _ => throw new InvalidOperationException("Unsupported action type.")
            };

            pendingAction.Status = result.Status == PendingConciergeActionStatus.Failed
                ? PendingConciergeActionStatus.Failed
                : result.RequiresHostApproval
                    ? PendingConciergeActionStatus.AwaitingHostApproval
                    : PendingConciergeActionStatus.Completed;
            pendingAction.ExecutedAt = DateTimeOffset.UtcNow;

            if (result.Status == PendingConciergeActionStatus.Failed)
            {
                pendingAction.FailureReasonCode = result.FailureCode ?? result.GuestSafeResultCode;
            }

            if (result.Status != PendingConciergeActionStatus.Failed)
            {
                await dbContext.ActionNotificationOutbox.AddAsync(new ActionNotificationOutbox
                {
                    Id = Guid.NewGuid(),
                    CompanyId = pendingAction.CompanyId,
                    ActionId = pendingAction.Id,
                    NotificationType = pendingAction.ActionType.ToString(),
                    PayloadReference = $"action:{pendingAction.Id:N}",
                    Status = ActionNotificationOutboxStatus.Pending,
                    AttemptCount = 0,
                    NextAttemptAt = DateTimeOffset.UtcNow
                }, cancellationToken);
            }

            await auditService.WriteAsync(
                pendingAction.CompanyId,
                pendingAction.ConversationId,
                pendingAction.Id,
                pendingAction.ActionType,
                result.Status == PendingConciergeActionStatus.Failed
                    ? ConciergeActionAuditEventType.ExecutionFailed
                    : ConciergeActionAuditEventType.ExecutionSucceeded,
                "System",
                tenantContext.UserId,
                "Chat",
                result.GuestSafeResultCode,
                tenantContext.CorrelationId ?? "none",
                null,
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            pendingAction.Status = PendingConciergeActionStatus.Failed;
            pendingAction.FailureReasonCode = "PersistenceFailed";
            await auditService.WriteAsync(
                pendingAction.CompanyId,
                pendingAction.ConversationId,
                pendingAction.Id,
                pendingAction.ActionType,
                ConciergeActionAuditEventType.ExecutionFailed,
                "System",
                tenantContext.UserId,
                "Chat",
                ConciergeActionResponseCodes.TemporaryFailure,
                tenantContext.CorrelationId ?? "none",
                null,
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
            return new ConciergeActionExecutionResult(
                pendingAction.Id,
                pendingAction.ActionType,
                PendingConciergeActionStatus.Failed,
                false,
                false,
                null,
                false,
                false,
                ConciergeActionResponseCodes.TemporaryFailure,
                "PersistenceFailed",
                null);
        }
    }
}
