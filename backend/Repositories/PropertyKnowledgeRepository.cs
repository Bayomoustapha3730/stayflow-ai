using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.PropertyKnowledge;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class PropertyKnowledgeRepository(ApplicationDbContext dbContext) : IPropertyKnowledgeRepository
{
    public Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
    {
        return dbContext.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(property => property.Id == propertyId && property.CompanyId == companyId && !property.IsDeleted, cancellationToken);
    }

    public async Task<PagedResult<PropertyKnowledgeArticle>> GetPagedAsync(Guid companyId, Guid propertyId, PropertyKnowledgeListQuery query, CancellationToken cancellationToken)
    {
        var pageNumber = query.NormalizedPageNumber;
        var pageSize = query.NormalizedPageSize;
        var itemsQuery = BaseQuery(companyId, propertyId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            itemsQuery = itemsQuery.Where(item =>
                EF.Functions.ILike(item.Title, $"%{search}%")
                || EF.Functions.ILike(item.Content, $"%{search}%")
                || EF.Functions.ILike(item.Tags, $"%{search}%"));
        }

        if (query.Category is { } category)
        {
            itemsQuery = itemsQuery.Where(item => item.Category == category);
        }

        if (query.IsApproved is { } isApproved)
        {
            itemsQuery = itemsQuery.Where(item => item.IsApproved == isApproved);
        }

        if (query.IsActive is { } isActive)
        {
            itemsQuery = itemsQuery.Where(item => item.IsActive == isActive);
        }

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var items = await itemsQuery
            .OrderByDescending(item => item.Priority)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Title)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(item => item.Property)
            .Include(item => item.ApprovedByUser)
            .ToListAsync(cancellationToken);

        return new PagedResult<PropertyKnowledgeArticle>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<PropertyKnowledgeArticle?> GetByIdAsync(Guid companyId, Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
    {
        return BaseQuery(companyId, propertyId)
            .Include(item => item.Property)
            .Include(item => item.ApprovedByUser)
            .FirstOrDefaultAsync(item => item.Id == knowledgeId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PropertyKnowledgeArticle>> GetApprovedActiveForPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
    {
        return await BaseQuery(companyId, propertyId)
            .Where(item => item.IsApproved && item.IsActive)
            .OrderByDescending(item => item.Priority)
            .ThenByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PropertyKnowledgeArticle article, CancellationToken cancellationToken)
    {
        await dbContext.PropertyKnowledgeArticles.AddAsync(article, cancellationToken);
    }

    public async Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<PropertyKnowledgeArticle> BaseQuery(Guid companyId, Guid propertyId)
    {
        return dbContext.PropertyKnowledgeArticles
            .Where(item => item.CompanyId == companyId && item.PropertyId == propertyId && !item.IsDeleted);
    }
}