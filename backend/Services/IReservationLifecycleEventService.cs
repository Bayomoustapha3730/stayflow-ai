using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed record ReservationLifecycleEventCreationResult(ReservationLifecycleEvent Event, bool WasNewlyCreated);

/// <summary>
/// Manages durable ReservationLifecycleEvent records only. Does not generate events on a schedule,
/// does not scan reservations, and never sends guest communication. See Slice 4 for automation.
/// </summary>
public interface IReservationLifecycleEventService
{
    Task<ReservationLifecycleEventCreationResult> TryCreateAsync(
        Reservation reservation,
        Property property,
        ReservationLifecycleEventType eventType,
        DateOnly propertyLocalDate,
        CancellationToken cancellationToken);

    Task<ReservationLifecycleEvent?> GetAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ReservationLifecycleEvent>> GetPendingAsync(Guid companyId, DateTimeOffset dueBeforeUtc, int limit, CancellationToken cancellationToken);

    Task<ReservationLifecycleEvent> MarkProcessingAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken);

    Task<ReservationLifecycleEvent> MarkProcessedAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken);

    Task<ReservationLifecycleEvent> MarkFailedAsync(Guid companyId, Guid eventId, string error, CancellationToken cancellationToken);
}
