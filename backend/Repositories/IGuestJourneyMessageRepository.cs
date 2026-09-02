using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IGuestJourneyMessageRepository
{
    Task<ReservationLifecycleEvent?> GetLifecycleEventContextAsync(Guid companyId, Guid lifecycleEventId, CancellationToken cancellationToken);
    Task<GuestJourneyMessage?> GetByLifecycleEventAsync(Guid companyId, Guid lifecycleEventId, CancellationToken cancellationToken);
    Task<Conversation?> GetLatestConversationForReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken);
    Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken);
    Task AddAsync(GuestJourneyMessage message, CancellationToken cancellationToken);
    void Detach(GuestJourneyMessage message);
    Task<IReadOnlyCollection<GuestJourneyMessage>> ClaimDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken);
    Task<int> RecoverStaleProcessingAsync(DateTimeOffset staleBeforeUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task<int> RecoverRetryableFailedAsync(DateTimeOffset nowUtc, int maxAttempts, CancellationToken cancellationToken);
    Task<ReservationLifecycleEvent?> GetLifecycleEventForDeliveryAsync(GuestJourneyMessage message, CancellationToken cancellationToken);
    Task<GuestJourneyMessage?> FindByConversationMessageAsync(Guid companyId, Guid conversationMessageId, CancellationToken cancellationToken);
    Task<WhatsAppIntegration?> GetActiveWhatsAppIntegrationAsync(Guid companyId, CancellationToken cancellationToken);
    Task MarkAcceptedAsync(GuestJourneyMessage message, Guid conversationMessageId, string? providerMessageId, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task MarkFailedAsync(GuestJourneyMessage message, string error, DateTimeOffset nextAttemptAtUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task MarkBlockedAsync(GuestJourneyMessage message, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task MarkSuppressedAsync(GuestJourneyMessage message, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}