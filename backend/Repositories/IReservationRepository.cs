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
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
