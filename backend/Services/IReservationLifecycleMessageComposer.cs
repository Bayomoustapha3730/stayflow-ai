using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed record GuestJourneyMessageContentSpec(string Language, string RenderedContent);

/// <summary>
/// Deterministically composes lifecycle guest-journey content from already-loaded, tenant-scoped
/// data. Does not persist and does not send. AI is intentionally not involved in Slice 5.
/// </summary>
public interface IReservationLifecycleMessageComposer
{
    GuestJourneyMessageContentSpec Compose(
        ReservationLifecycleEvent lifecycleEvent,
        Reservation reservation,
        Property property,
        Guest guest);
}
