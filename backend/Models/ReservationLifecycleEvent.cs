namespace StayFlow.Api.Models;

/// <summary>
/// Durable occurrence/processing record for reservation lifecycle automation.
/// This is NOT the reservation's current lifecycle stage, which remains derived
/// (see IReservationLifecycleService) and must never be persisted on Reservation.
/// </summary>
public sealed class ReservationLifecycleEvent : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid GuestId { get; set; }
    public ReservationLifecycleEventType EventType { get; set; }
    public string RuleVersion { get; set; } = string.Empty;
    // Property-local calendar date the event is anchored to; participates in idempotency identity
    // because ScheduledForUtc alone would drift if timezone data changes after creation.
    public DateOnly PropertyLocalDate { get; set; }
    public DateTimeOffset ScheduledForUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public ReservationLifecycleEventStatus Status { get; set; } = ReservationLifecycleEventStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;

    public Company Company { get; set; } = null!;
    public Reservation Reservation { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Guest Guest { get; set; } = null!;
}
