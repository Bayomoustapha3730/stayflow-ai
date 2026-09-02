using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

/// <summary>
/// Delivery-only worker for durable GuestJourneyMessage outbox rows. Never invoked directly by the
/// reservation lifecycle worker/processor: lifecycle processing durably creates the communication
/// intent, and this processor independently claims and attempts delivery, so a WhatsApp outage never
/// blocks lifecycle event completion.
/// </summary>
public sealed class GuestJourneyMessageDeliveryProcessor(
    IGuestJourneyMessageRepository repository,
    IConversationService conversationService,
    Repositories.IConversationRepository conversationRepository,
    IWhatsAppCustomerServiceWindowEvaluator windowEvaluator,
    IReservationLifecycleWhatsAppTemplateResolver templateResolver,
    IWhatsAppTemplateService whatsAppTemplateService,
    IReservationLifecycleEventIdempotencyKeyBuilder idempotencyKeyBuilder,
    TimeProvider timeProvider,
    IOptions<GuestJourneyDeliveryOptions> deliveryOptions,
    IOptions<ReservationContextOptions> reservationContextOptions,
    ILogger<GuestJourneyMessageDeliveryProcessor> logger) : IGuestJourneyMessageDeliveryProcessor
{
    // Failure codes assigned directly by WhatsAppConversationChannelSender before any provider call;
    // these represent configuration/session issues that will not resolve by simply retrying.
    private static readonly HashSet<string> BlockedFailureCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "MissingIntegration",
        "ProductionDisabled",
        "CustomerServiceWindowClosed",
        "CredentialResolutionFailed",
        "InvalidRecipient"
    };

    public async Task<GuestJourneyMessageDeliveryResult> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var options = deliveryOptions.Value;
        var nowUtc = timeProvider.GetUtcNow();
        var staleBeforeUtc = nowUtc.Subtract(TimeSpan.FromMinutes(options.ProcessingLeaseTimeoutMinutes));

        var staleRecovered = await repository.RecoverStaleProcessingAsync(staleBeforeUtc, nowUtc, cancellationToken);
        var failedRecovered = await repository.RecoverRetryableFailedAsync(nowUtc, options.MaxAttempts, cancellationToken);
        var claimed = await repository.ClaimDueAsync(nowUtc, options.BatchSize, cancellationToken);

        var accepted = 0;
        var failed = 0;
        var suppressed = 0;
        var blocked = 0;

        foreach (var message in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var lifecycleEvent = await repository.GetLifecycleEventForDeliveryAsync(message, cancellationToken);
                var suppressionReason = GetSuppressionReason(message, lifecycleEvent);
                if (suppressionReason is not null)
                {
                    await repository.MarkSuppressedAsync(message, suppressionReason, nowUtc, cancellationToken);
                    suppressed++;
                    continue;
                }

                if (message.ConversationId is null)
                {
                    await repository.MarkBlockedAsync(message, "Guest journey message is missing a resolved conversation.", nowUtc, cancellationToken);
                    blocked++;
                    continue;
                }

                var integration = await repository.GetActiveWhatsAppIntegrationAsync(message.CompanyId, cancellationToken);
                if (integration is null)
                {
                    await repository.MarkBlockedAsync(message, "WhatsApp integration is not configured for this company.", nowUtc, cancellationToken);
                    blocked++;
                    continue;
                }

                // The evaluator returns IsOpen=false whenever it cannot establish a recent inbound
                // message, so an indeterminate window already fails closed here (never free-form).
                var window = await windowEvaluator.EvaluateAsync(message.CompanyId, message.ConversationId.Value, cancellationToken);

                ApiResponse<ConversationMessageResponse>? sendResult;
                if (window.IsOpen)
                {
                    sendResult = await conversationService.AddLifecycleAutomationMessageAsync(
                        message.CompanyId,
                        message.ConversationId.Value,
                        message.RenderedContent,
                        message.IdempotencyKey,
                        cancellationToken);
                }
                else
                {
                    var reservation = lifecycleEvent!.Reservation;
                    var property = lifecycleEvent.Property;
                    var guest = lifecycleEvent.Guest;

                    var resolution = await templateResolver.ResolveAsync(
                        message.CompanyId,
                        integration.Id,
                        message.JourneyEventType,
                        guest.PreferredLanguage,
                        reservation,
                        property,
                        guest,
                        cancellationToken);

                    if (!resolution.Resolved)
                    {
                        await repository.MarkBlockedAsync(message, resolution.BlockedReason ?? "No configured approved lifecycle WhatsApp template is available.", nowUtc, cancellationToken);
                        blocked++;
                        continue;
                    }

                    sendResult = await whatsAppTemplateService.SendLifecycleAutomationTemplateMessageAsync(
                        message.CompanyId,
                        message.ConversationId.Value,
                        integration.Id,
                        resolution.Template!.Id,
                        resolution.Variables,
                        message.IdempotencyKey,
                        cancellationToken);
                }

                if (!sendResult.Success || sendResult.Data is null)
                {
                    await repository.MarkFailedAsync(message, sendResult.Message ?? "Failed to store lifecycle automation message.", nowUtc.AddMinutes(options.RetryDelayMinutes), nowUtc, cancellationToken);
                    failed++;
                    continue;
                }

                var conversationMessage = await conversationRepository.GetMessageForConversationAsync(message.CompanyId, message.ConversationId.Value, sendResult.Data.Id, cancellationToken);
                if (conversationMessage is null)
                {
                    await repository.MarkFailedAsync(message, "Conversation message could not be located after creation.", nowUtc.AddMinutes(options.RetryDelayMinutes), nowUtc, cancellationToken);
                    failed++;
                    continue;
                }

                if (conversationMessage.DeliveryStatus == ConversationMessageDeliveryStatus.Sent)
                {
                    await repository.MarkAcceptedAsync(message, conversationMessage.Id, conversationMessage.ExternalMessageId, nowUtc, cancellationToken);
                    accepted++;
                    continue;
                }

                var failureSummary = conversationMessage.FailureReason ?? conversationMessage.FailureCode ?? "WhatsApp delivery failed.";
                if (conversationMessage.FailureCode is not null && BlockedFailureCodes.Contains(conversationMessage.FailureCode))
                {
                    await repository.MarkBlockedAsync(message, failureSummary, nowUtc, cancellationToken);
                    blocked++;
                }
                else
                {
                    await repository.MarkFailedAsync(message, failureSummary, nowUtc.AddMinutes(options.RetryDelayMinutes), nowUtc, cancellationToken);
                    failed++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(
                    ex,
                    "Guest journey message delivery failed. MessageId={MessageId} CompanyId={CompanyId} ReservationId={ReservationId} ReservationLifecycleEventId={ReservationLifecycleEventId} AttemptCount={AttemptCount}.",
                    message.Id,
                    message.CompanyId,
                    message.ReservationId,
                    message.ReservationLifecycleEventId,
                    message.AttemptCount);

                await repository.MarkFailedAsync(message, ex.Message, nowUtc.AddMinutes(options.RetryDelayMinutes), nowUtc, cancellationToken);
            }
        }

        return new GuestJourneyMessageDeliveryResult(staleRecovered, failedRecovered, claimed.Count, accepted, failed, suppressed, blocked);
    }

    private string? GetSuppressionReason(GuestJourneyMessage message, ReservationLifecycleEvent? lifecycleEvent)
    {
        if (lifecycleEvent is null)
        {
            return "Guest journey message suppressed because the reservation lifecycle event no longer exists for this tenant.";
        }

        var reservation = lifecycleEvent.Reservation;
        if (reservation is null
            || reservation.CompanyId != message.CompanyId
            || reservation.PropertyId != message.PropertyId
            || reservation.PrimaryGuestId != message.GuestId)
        {
            return "Guest journey message suppressed because reservation identity no longer matches the message.";
        }

        if (!IsEligibleReservationStatus(reservation.Status))
        {
            return $"Guest journey message suppressed because reservation status is {reservation.Status}.";
        }

        var currentKeys = ReservationLifecycleEventGenerator.BuildAnchors(reservation, reservationContextOptions.Value.PreArrivalWindowDays)
            .Select(anchor => idempotencyKeyBuilder.Build(reservation.CompanyId, reservation.Id, anchor.EventType, anchor.PropertyLocalDate, lifecycleEvent.RuleVersion))
            .ToHashSet(StringComparer.Ordinal);

        if (!currentKeys.Contains(lifecycleEvent.IdempotencyKey))
        {
            return "Guest journey message suppressed because reservation dates no longer match the originating lifecycle event.";
        }

        return null;
    }

    private static bool IsEligibleReservationStatus(ReservationStatus status)
    {
        return status is ReservationStatus.Confirmed
            or ReservationStatus.PreArrival
            or ReservationStatus.ReadyForCheckIn
            or ReservationStatus.CheckedIn
            or ReservationStatus.ActiveStay
            or ReservationStatus.CheckOutPending
            or ReservationStatus.CheckedOut
            or ReservationStatus.PostStay;
    }
}
