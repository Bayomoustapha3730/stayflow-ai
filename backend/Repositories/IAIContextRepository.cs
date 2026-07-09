using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IAIContextRepository
{
    Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken);
    Task<int> CountCompletedReservationsForGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken);
    Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken);
    Task<Property?> GetPropertyContextAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken);
    Task<Conversation?> GetConversationAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
