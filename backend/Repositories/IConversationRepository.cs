using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IConversationRepository
{
    Task<PagedResult<ConversationSummaryResponse>> ListConversationsAsync(Guid companyId, ConversationListQueryParameters query, CancellationToken cancellationToken);
    Task<int> GetTotalUnreadCountForHostAsync(Guid companyId, Guid hostUserId, CancellationToken cancellationToken);
    Task<Dictionary<Guid, int>> GetUnreadMessageCountsForHostAsync(Guid companyId, Guid hostUserId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken);
    Task<int> GetUnreadHostMessageCountForGuestAsync(Guid companyId, Guid guestId, Guid conversationId, CancellationToken cancellationToken);
    Task<Conversation?> GetByIdForCompanyAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken);
    Task<ConversationMessage?> GetMessageForConversationAsync(Guid companyId, Guid conversationId, Guid messageId, CancellationToken cancellationToken);
    Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, Guid? reservationId, Guid? propertyId, DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>
    /// Find an open conversation for enrichment. Used by WhatsApp inbound processor to bind/enrich unbound conversations.
    /// First tries exact match (same reservationId), then falls back to unbound conversations from same integration.
    /// </summary>
    Task<Conversation?> GetOpenConversationForEnrichmentAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, Guid? reservationId, Guid? propertyId, Guid? whatsAppIntegrationId, DateTimeOffset cutoff, CancellationToken cancellationToken)
        => GetOpenConversationAsync(companyId, guestId, channel, channelIdentity, reservationId, propertyId, cutoff, cancellationToken);
    // Used by post-payment notifications to find the conversation to post a confirmation into.
    // Defaults to null so existing test fakes don't need to implement it.
    Task<Conversation?> GetLatestConversationForReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken)
        => Task.FromResult<Conversation?>(null);
    Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid companyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken);
    Task<ConversationMessage?> GetLatestVisibleMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken);
    Task<ConversationParticipantReadState?> GetReadStateAsync(Guid companyId, Guid conversationId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ConversationParticipantReadState>> GetReadStatesForParticipantAsync(Guid companyId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken);
    Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, ConversationMessageProvider? provider, CancellationToken cancellationToken);
    Task<ConversationMessage?> FindByIdempotencyKeyAsync(Guid companyId, string idempotencyKey, CancellationToken cancellationToken)
        => Task.FromResult<ConversationMessage?>(null);
    // Persists a brand-new automated-template ConversationMessage that owns a not-yet-seen
    // CompanyId+IdempotencyKey. If a concurrent caller wins the race for the same key, the losing
    // caller must not proceed to a provider send: it reloads and returns the winner's row instead.
    // Default implementation (used by non-EF test fakes) has no unique-constraint protection and
    // always "claims" successfully; only the real EF-backed repository enforces the race guarantee.
    async Task<(ConversationMessage Message, bool Claimed)> ClaimAutomatedTemplateMessageAsync(ConversationMessage candidate, CancellationToken cancellationToken)
    {
        await AddMessageAsync(candidate, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return (candidate, true);
    }
    Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken);
    Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken);
    Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken);
    Task<User?> GetUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken);
    Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken);
    Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken);
    Task AddMessageKnowledgeSourceAsync(ConversationMessageKnowledgeSource source, CancellationToken cancellationToken) => Task.CompletedTask;
    Task<ConversationMessageFeedback?> GetMessageFeedbackAsync(Guid companyId, Guid conversationId, Guid messageId, Guid guestId, CancellationToken cancellationToken)
        => Task.FromResult<ConversationMessageFeedback?>(null);
    Task AddMessageFeedbackAsync(ConversationMessageFeedback feedback, CancellationToken cancellationToken) => Task.CompletedTask;
    Task<IReadOnlyCollection<ConversationMessageFeedback>> ListMessageFeedbackAsync(Guid companyId, DateTimeOffset sinceUtc, DateTimeOffset untilUtc, Guid? propertyId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<ConversationMessageFeedback>>([]);
    Task AddReadStateAsync(ConversationParticipantReadState state, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
