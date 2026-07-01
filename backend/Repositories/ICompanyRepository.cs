using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Companies;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface ICompanyRepository
{
    Task<PagedResult<Company>> GetAsync(CompanyQueryParameters query, CancellationToken cancellationToken);
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Company company, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
