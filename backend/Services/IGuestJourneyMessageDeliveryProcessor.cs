namespace StayFlow.Api.Services;

public sealed record GuestJourneyMessageDeliveryResult(
    int StaleRecovered,
    int FailedRecovered,
    int Claimed,
    int Accepted,
    int Failed,
    int Suppressed,
    int Blocked);

/// <summary>
/// Claims durable GuestJourneyMessage outbox rows and delivers them through the existing
/// conversation/WhatsApp infrastructure. A lifecycle event being Processed only means the
/// communication intent was durably created (see ReservationLifecycleGuestJourneyHandler);
/// this processor is the separate, independently retried step that attempts actual delivery.
/// </summary>
public interface IGuestJourneyMessageDeliveryProcessor
{
    Task<GuestJourneyMessageDeliveryResult> ProcessDueAsync(CancellationToken cancellationToken);
}
