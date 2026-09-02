using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

/// <summary>
/// Production IReservationLifecycleEventHandler. Creates the durable GuestJourneyMessage
/// communication intent only; it never sends WhatsApp or touches ChatService/AI. A lifecycle
/// event is considered handled once the intent exists durably, not once it is delivered.
/// </summary>
public sealed class ReservationLifecycleGuestJourneyHandler(
    IGuestJourneyMessageRepository guestJourneyMessageRepository,
    IGuestJourneyMessageService guestJourneyMessageService,
    IReservationLifecycleMessageComposer messageComposer,
    IConversationService conversationService,
    ITenantExecutionContextAccessor tenantExecutionContextAccessor,
    ILogger<ReservationLifecycleGuestJourneyHandler> logger) : IReservationLifecycleEventHandler
{
    public async Task HandleAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        var hydrated = await guestJourneyMessageRepository.GetLifecycleEventContextAsync(lifecycleEvent.CompanyId, lifecycleEvent.Id, cancellationToken);
        if (hydrated?.Reservation is null || hydrated.Property is null || hydrated.Guest is null)
        {
            logger.LogWarning(
                "Reservation lifecycle guest journey handler skipped EventId={EventId} CompanyId={CompanyId} because tenant-scoped context could not be loaded.",
                lifecycleEvent.Id,
                lifecycleEvent.CompanyId);
            return;
        }

        var reservation = hydrated.Reservation;
        var property = hydrated.Property;
        var guest = hydrated.Guest;

        if (reservation.CompanyId != lifecycleEvent.CompanyId
            || reservation.PropertyId != lifecycleEvent.PropertyId
            || reservation.PrimaryGuestId != lifecycleEvent.GuestId)
        {
            logger.LogWarning(
                "Reservation lifecycle guest journey handler skipped EventId={EventId} CompanyId={CompanyId} ReservationId={ReservationId} because reservation identity no longer matches the event.",
                lifecycleEvent.Id,
                lifecycleEvent.CompanyId,
                lifecycleEvent.ReservationId);
            return;
        }

        var conversationId = await ResolveConversationIdAsync(reservation, guest, cancellationToken);
        var spec = messageComposer.Compose(hydrated, reservation, property, guest);

        await guestJourneyMessageService.TryCreateAsync(hydrated, spec.Language, spec.RenderedContent, conversationId, cancellationToken);
    }

    private async Task<Guid?> ResolveConversationIdAsync(Reservation reservation, Guest guest, CancellationToken cancellationToken)
    {
        var existing = await guestJourneyMessageRepository.GetLatestConversationForReservationAsync(reservation.CompanyId, reservation.Id, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        // Reuses the existing tenant-scoped conversation creation path (same convention as
        // WhatsAppWebhookProcessor) instead of a parallel conversation architecture. Residual race:
        // two concurrent handler runs for the same reservation could each create a conversation here;
        // the GuestJourneyMessage unique constraint still guarantees exactly one durable message.
        tenantExecutionContextAccessor.Set(reservation.CompanyId, null, $"lifecycle:{reservation.Id:N}");
        try
        {
            var result = await conversationService.CreateOrGetConversationAsync(new CreateConversationRequest
            {
                GuestId = guest.Id,
                ReservationId = reservation.Id,
                PropertyId = reservation.PropertyId,
                Channel = GuestChannel.WhatsApp,
                Subject = "Reservation lifecycle automation"
            }, cancellationToken);

            return result.Success ? result.Data?.Id : null;
        }
        finally
        {
            tenantExecutionContextAccessor.Clear();
        }
    }
}
