using StayFlow.Api.Common;
using StayFlow.Api.DTOs.PropertyKnowledge;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IPropertyKnowledgeRepository
{
    Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken);
    Task<PagedResult<PropertyKnowledgeArticle>> GetPagedAsync(Guid companyId, Guid propertyId, PropertyKnowledgeListQuery query, CancellationToken cancellationToken);
    Task<PropertyKnowledgeArticle?> GetByIdAsync(Guid companyId, Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PropertyKnowledgeArticle>> GetApprovedActiveForPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken);
    Task AddAsync(PropertyKnowledgeArticle article, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}