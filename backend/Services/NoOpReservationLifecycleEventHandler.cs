using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class NoOpReservationLifecycleEventHandler(ILogger<NoOpReservationLifecycleEventHandler> logger) : IReservationLifecycleEventHandler
{
    public Task HandleAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "No-op reservation lifecycle event handled. EventId={EventId} CompanyId={CompanyId} ReservationId={ReservationId} EventType={EventType} ScheduledForUtc={ScheduledForUtc}.",
            lifecycleEvent.Id,
            lifecycleEvent.CompanyId,
            lifecycleEvent.ReservationId,
            lifecycleEvent.EventType,
            lifecycleEvent.ScheduledForUtc);

        return Task.CompletedTask;
    }
}