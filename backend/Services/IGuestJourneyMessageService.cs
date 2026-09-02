using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed record GuestJourneyMessageCreationResult(GuestJourneyMessage Message, bool WasNewlyCreated);

/// <summary>
/// Idempotently creates the durable GuestJourneyMessage communication intent for a lifecycle
/// event. Does not send anything; delivery is a separate, independently retried concern (see
/// IGuestJourneyMessageDeliveryProcessor).
/// </summary>
public interface IGuestJourneyMessageService
{
    Task<GuestJourneyMessageCreationResult> TryCreateAsync(
        ReservationLifecycleEvent lifecycleEvent,
        string language,
        string renderedContent,
        Guid? conversationId,
        CancellationToken cancellationToken);
}
