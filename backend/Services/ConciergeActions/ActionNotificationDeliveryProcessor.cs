using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services.ConciergeActions;

/// <summary>
/// Delivery-only worker for the ActionNotificationOutbox: sends the guest-facing message that
/// closes the loop after a host approves/declines a concierge action. Independent of the
/// synchronous guest acknowledgement already sent at submission time - only "HostApproved" and
/// "HostDeclined" notification types produce a guest message here; other notification types are
/// marked delivered as a no-op since the guest was already replied to in the same chat turn.
/// </summary>
public sealed class ActionNotificationDeliveryProcessor(
    IActionNotificationOutboxRepository repository,
    IConversationService conversationService,
    TimeProvider timeProvider,
    IOptions<ActionNotificationDeliveryOptions> options,
    ILogger<ActionNotificationDeliveryProcessor> logger,
    IWhatsAppTemplateService? whatsAppTemplateService = null) : IActionNotificationDeliveryProcessor
{
    private const string HostApprovedNotificationType = "HostApproved";
    private const string HostDeclinedNotificationType = "HostDeclined";

    public async Task<ActionNotificationDeliveryResult> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var due = await repository.GetDueAsync(nowUtc, options.Value.BatchSize, cancellationToken);

        var sent = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var outbox in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pendingAction = await repository.GetPendingActionAsync(outbox.CompanyId, outbox.ActionId, cancellationToken);
            if (pendingAction is null)
            {
                repository.MarkTerminalFailure(outbox, "ActionNotFound");
                failed++;
                continue;
            }

            var guestMessage = BuildGuestMessage(outbox.NotificationType, pendingAction.ActionType);
            if (guestMessage is null)
            {
                // Not a guest-facing notification type; nothing further to deliver.
                repository.MarkSent(outbox, nowUtc);
                skipped++;
                continue;
            }

            try
            {
                var idempotencyKey = $"action-notification:{outbox.Id:N}:freeform";
                var response = await conversationService.AddLifecycleAutomationMessageAsync(
                    outbox.CompanyId,
                    pendingAction.ConversationId,
                    guestMessage,
                    idempotencyKey,
                    cancellationToken);

                if (response.Success
                    && response.Data?.DeliveryStatus == ConversationMessageDeliveryStatus.Failed
                    && response.Data.FailureCode == "CustomerServiceWindowClosed")
                {
                    if (whatsAppTemplateService is not null)
                    {
                        response = await whatsAppTemplateService.SendHostActionTemplateMessageAsync(
                            outbox.CompanyId,
                            pendingAction.ConversationId,
                            pendingAction.ActionType,
                            outbox.NotificationType,
                            $"action-notification:{outbox.Id:N}:template",
                            cancellationToken);
                    }
                }

                if (response.Success && response.Data?.DeliveryStatus is
                    ConversationMessageDeliveryStatus.Sent or
                    ConversationMessageDeliveryStatus.Delivered or
                    ConversationMessageDeliveryStatus.Read)
                {
                    repository.MarkSent(outbox, nowUtc);
                    sent++;
                }
                else
                {
                    // Message persistence can succeed while the underlying WhatsApp send still
                    // fails (e.g. the 24-hour customer-service window is closed); treat that the
                    // same as a send failure instead of falsely marking the outbox delivered.
                    var failureCode = response.Success
                        ? (response.Data?.FailureCode ?? response.Data?.SafeFailureSummary ?? "DeliveryFailed")
                        : response.Message;
                    MarkFailure(outbox, nowUtc, failureCode);
                    failed++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error delivering action notification {ActionNotificationId}.", outbox.Id);
                MarkFailure(outbox, nowUtc, "UnexpectedError");
                failed++;
            }
        }

        if (due.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return new ActionNotificationDeliveryResult(due.Count, sent, failed, skipped);
    }

    private void MarkFailure(ActionNotificationOutbox outbox, DateTimeOffset nowUtc, string failureCode)
    {
        if (outbox.AttemptCount + 1 >= options.Value.MaxAttempts)
        {
            repository.MarkTerminalFailure(outbox, failureCode);
            return;
        }

        repository.MarkRetry(outbox, failureCode, nowUtc.AddMinutes(options.Value.RetryDelayMinutes));
    }

    private static string? BuildGuestMessage(string notificationType, ConciergeActionType actionType)
    {
        var label = ActionLabel(actionType);

        return notificationType switch
        {
            HostApprovedNotificationType => $"Good news! Your {label} request has been approved.",
            HostDeclinedNotificationType => $"We're sorry, your {label} request could not be approved at this time.",
            _ => null
        };
    }

    private static string ActionLabel(ConciergeActionType actionType) => actionType switch
    {
        ConciergeActionType.RequestEarlyCheckIn => "early check-in",
        ConciergeActionType.RequestLateCheckout => "late checkout",
        ConciergeActionType.RequestParking => "parking",
        ConciergeActionType.RequestHousekeeping => "housekeeping",
        ConciergeActionType.RequestExtraItem => "extra item",
        ConciergeActionType.CreateMaintenanceTicket => "maintenance",
        _ => "service"
    };
}
