using Microsoft.Extensions.Options;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class ReservationLifecycleEventProcessor(
    IReservationLifecycleEventRepository repository,
    IReservationLifecycleEventService lifecycleEventService,
    IReservationLifecycleEventHandler handler,
    IReservationLifecycleEventIdempotencyKeyBuilder idempotencyKeyBuilder,
    TimeProvider timeProvider,
    IOptions<ReservationLifecycleEventOptions> lifecycleEventOptions,
    IOptions<ReservationContextOptions> reservationContextOptions,
    ILogger<ReservationLifecycleEventProcessor> logger) : IReservationLifecycleEventProcessor
{
    public async Task<ReservationLifecycleEventProcessingResult> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var options = lifecycleEventOptions.Value;
        var nowUtc = timeProvider.GetUtcNow();
        var staleBeforeUtc = nowUtc.Subtract(TimeSpan.FromMinutes(options.ProcessingLeaseTimeoutMinutes));
        var retryBeforeUtc = nowUtc.Subtract(TimeSpan.FromMinutes(options.RetryDelayMinutes));

        var staleRecovered = await repository.RecoverStaleProcessingAsync(staleBeforeUtc, nowUtc, cancellationToken);
        var failedRecovered = await repository.RecoverRetryableFailedAsync(retryBeforeUtc, nowUtc, options.MaxAttempts, cancellationToken);
        var claimedEvents = await repository.ClaimDueAsync(nowUtc, options.ProcessingBatchSize, cancellationToken);

        var processed = 0;
        var failed = 0;
        var suppressed = 0;

        foreach (var lifecycleEvent in claimedEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var reservation = await repository.GetReservationForEventAsync(lifecycleEvent, cancellationToken);
                var suppressionReason = GetSuppressionReason(lifecycleEvent, reservation);
                if (suppressionReason is not null)
                {
                    await lifecycleEventService.MarkSuppressedAsync(lifecycleEvent.CompanyId, lifecycleEvent.Id, suppressionReason, cancellationToken);
                    suppressed++;
                    continue;
                }

                await handler.HandleAsync(lifecycleEvent, cancellationToken);
                await lifecycleEventService.MarkProcessedAsync(lifecycleEvent.CompanyId, lifecycleEvent.Id, cancellationToken);
                processed++;
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
                    "Reservation lifecycle event processing failed. EventId={EventId} CompanyId={CompanyId} ReservationId={ReservationId} PropertyId={PropertyId} GuestId={GuestId} EventType={EventType} AttemptCount={AttemptCount} ScheduledForUtc={ScheduledForUtc}.",
                    lifecycleEvent.Id,
                    lifecycleEvent.CompanyId,
                    lifecycleEvent.ReservationId,
                    lifecycleEvent.PropertyId,
                    lifecycleEvent.GuestId,
                    lifecycleEvent.EventType,
                    lifecycleEvent.AttemptCount,
                    lifecycleEvent.ScheduledForUtc);

                await lifecycleEventService.MarkFailedAsync(lifecycleEvent.CompanyId, lifecycleEvent.Id, ex.Message, cancellationToken);
            }
        }

        return new ReservationLifecycleEventProcessingResult(staleRecovered, failedRecovered, claimedEvents.Count, processed, failed, suppressed);
    }

    private string? GetSuppressionReason(ReservationLifecycleEvent lifecycleEvent, Reservation? reservation)
    {
        if (reservation is null)
        {
            return "Reservation lifecycle event suppressed because the reservation no longer exists for this tenant.";
        }

        if (reservation.CompanyId != lifecycleEvent.CompanyId
            || reservation.PropertyId != lifecycleEvent.PropertyId
            || reservation.PrimaryGuestId != lifecycleEvent.GuestId)
        {
            return "Reservation lifecycle event suppressed because reservation identity no longer matches the event.";
        }

        if (!IsEligibleReservationStatus(reservation.Status))
        {
            return $"Reservation lifecycle event suppressed because reservation status is {reservation.Status}.";
        }

        var currentKeys = ReservationLifecycleEventGenerator.BuildAnchors(reservation, reservationContextOptions.Value.PreArrivalWindowDays)
            .Select(anchor => idempotencyKeyBuilder.Build(reservation.CompanyId, reservation.Id, anchor.EventType, anchor.PropertyLocalDate, lifecycleEvent.RuleVersion))
            .ToHashSet(StringComparer.Ordinal);

        if (!currentKeys.Contains(lifecycleEvent.IdempotencyKey))
        {
            return "Reservation lifecycle event suppressed because reservation dates no longer match the event anchor.";
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