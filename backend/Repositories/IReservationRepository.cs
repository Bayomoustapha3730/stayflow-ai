using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Reservations;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IReservationRepository
{
    Task<PagedResult<Reservation>> GetAsync(Guid companyId, ReservationQueryParameters query, CancellationToken cancellationToken);
    Task<Reservation?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken);
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken);
    Task<bool> PropertyBelongsToCompanyAsync(Guid propertyId, Guid companyId, CancellationToken cancellationToken);
    Task<bool> GuestBelongsToCompanyAsync(Guid guestId, Guid companyId, CancellationToken cancellationToken);
    Task<Guest?> GetGuestAsync(Guid guestId, Guid companyId, CancellationToken cancellationToken);
    Task<Conversation?> GetConversationAsync(Guid conversationId, Guid companyId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsForGuestAsync(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsByReferenceAsync(Guid companyId, Guid guestId, string normalizedReference, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsByPropertyNameAsync(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate, string normalizedPropertyName, CancellationToken cancellationToken);
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
