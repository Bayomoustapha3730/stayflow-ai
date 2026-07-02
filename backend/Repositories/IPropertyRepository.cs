using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Properties;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IPropertyRepository
{
    Task<PagedResult<Property>> GetAsync(PropertyQueryParameters query, CancellationToken cancellationToken);
    Task<Property?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken);
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken);
    Task AddAsync(Property property, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
