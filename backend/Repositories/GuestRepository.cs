using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Guests;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class GuestRepository(ApplicationDbContext dbContext) : IGuestRepository
{
    public async Task<PagedResult<Guest>> GetAsync(Guid companyId, GuestQueryParameters query, CancellationToken cancellationToken)
    {
        var pageNumber = query.NormalizedPageNumber;
        var pageSize = query.NormalizedPageSize;

        var guestsQuery = dbContext.Guests
            .AsNoTracking()
            .Where(guest => guest.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim();
            guestsQuery = guestsQuery.Where(guest =>
                EF.Functions.ILike(guest.FirstName, $"%{searchTerm}%")
                || EF.Functions.ILike(guest.LastName, $"%{searchTerm}%")
                || (guest.Email != null && EF.Functions.ILike(guest.Email, $"%{searchTerm}%"))
                || (guest.PhoneNumber != null && EF.Functions.ILike(guest.PhoneNumber, $"%{searchTerm}%")));
        }

        var totalCount = await guestsQuery.CountAsync(cancellationToken);
        var items = await guestsQuery
            .OrderBy(guest => guest.LastName)
            .ThenBy(guest => guest.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Guest>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<Guest?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Guests
            .FirstOrDefaultAsync(guest => guest.Id == id && guest.CompanyId == companyId, cancellationToken);
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Companies.AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken);
    }

    public async Task AddAsync(Guest guest, CancellationToken cancellationToken)
    {
        await dbContext.Guests.AddAsync(guest, cancellationToken);
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
