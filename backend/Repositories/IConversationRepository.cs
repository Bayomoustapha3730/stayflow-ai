using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;
<<<<<<< HEAD
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
=======
>>>>>>> 297967c (Implement host conversation inbox endpoint)

namespace StayFlow.Api.Repositories;

public interface IConversationRepository
{
<<<<<<< HEAD
    Task<Conversation?> GetByIdForCompanyAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken);
    Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, DateTimeOffset cutoff, CancellationToken cancellationToken);
    Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid companyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken);
    Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, CancellationToken cancellationToken);
    Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken);
    Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken);
    Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken);
    Task<User?> GetUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken);
    Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken);
    Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
=======
    Task<PagedResult<ConversationSummaryResponse>> GetInboxAsync(
        Guid companyId,
        ConversationListQueryParameters query,
        CancellationToken cancellationToken);
>>>>>>> 297967c (Implement host conversation inbox endpoint)
}
