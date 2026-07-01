using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Companies;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class CompanyRepository(ApplicationDbContext dbContext) : ICompanyRepository
{
    public async Task<PagedResult<Company>> GetAsync(
        CompanyQueryParameters query,
        CancellationToken cancellationToken)
    {
        var pageNumber = query.NormalizedPageNumber;
        var pageSize = query.NormalizedPageSize;

        var companiesQuery = dbContext.Companies
            .AsNoTracking()
            .Where(company => company.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim();
            companiesQuery = companiesQuery.Where(company =>
                EF.Functions.ILike(company.Name, $"%{searchTerm}%"));
        }

        var totalCount = await companiesQuery.CountAsync(cancellationToken);
        var items = await companiesQuery
            .OrderBy(company => company.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Company>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Companies
            .FirstOrDefaultAsync(company => company.Id == id && company.IsActive, cancellationToken);
    }

    public async Task AddAsync(Company company, CancellationToken cancellationToken)
    {
        await dbContext.Companies.AddAsync(company, cancellationToken);
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
