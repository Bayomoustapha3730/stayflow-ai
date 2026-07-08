using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Properties;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class PropertyRepository(ApplicationDbContext dbContext) : IPropertyRepository
{
    public async Task<PagedResult<Property>> GetAsync(Guid companyId, PropertyQueryParameters query, CancellationToken cancellationToken)
    {
        var pageNumber = query.NormalizedPageNumber;
        var pageSize = query.NormalizedPageSize;

        var propertiesQuery = dbContext.Properties
            .AsNoTracking()
            .Where(property => property.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim();
            propertiesQuery = propertiesQuery.Where(property => EF.Functions.ILike(property.Name, $"%{searchTerm}%"));
        }

        var totalCount = await propertiesQuery.CountAsync(cancellationToken);
        var items = await propertiesQuery
            .OrderBy(property => property.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Property>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<Property?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Properties
            .Include(property => property.PropertyAmenities)
            .Include(property => property.PropertyHouseRules)
            .Include(property => property.PropertyRecommendations)
            .Include(property => property.PropertyEmergencyContacts)
            .Include(property => property.PropertyKnowledgeArticles)
            .FirstOrDefaultAsync(property => property.Id == id && property.CompanyId == companyId, cancellationToken);
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Companies.AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken);
    }

    public async Task AddAsync(Property property, CancellationToken cancellationToken)
    {
        await dbContext.Properties.AddAsync(property, cancellationToken);
    }

    public async Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
