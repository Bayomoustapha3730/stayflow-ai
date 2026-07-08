using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Guests;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IGuestRepository
{
    Task<PagedResult<Guest>> GetAsync(Guid companyId, GuestQueryParameters query, CancellationToken cancellationToken);
    Task<Guest?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken);
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken);
    Task AddAsync(Guest guest, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
