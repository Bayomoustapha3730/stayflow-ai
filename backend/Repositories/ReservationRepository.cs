using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Reservations;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class ReservationRepository(ApplicationDbContext dbContext) : IReservationRepository
{
    public async Task<PagedResult<Reservation>> GetAsync(Guid companyId, ReservationQueryParameters query, CancellationToken cancellationToken)
    {
        var pageNumber = query.NormalizedPageNumber;
        var pageSize = query.NormalizedPageSize;

        var reservationsQuery = dbContext.Reservations
            .AsNoTracking()
            .Where(reservation => reservation.CompanyId == companyId);

        if (query.PropertyId is { } propertyId)
        {
            reservationsQuery = reservationsQuery.Where(reservation => reservation.PropertyId == propertyId);
        }

        if (query.PrimaryGuestId is { } primaryGuestId)
        {
            reservationsQuery = reservationsQuery.Where(reservation => reservation.PrimaryGuestId == primaryGuestId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<ReservationStatus>(query.Status.Trim(), ignoreCase: true, out var status))
            {
                reservationsQuery = reservationsQuery.Where(reservation => reservation.Status == status);
            }
            else
            {
                reservationsQuery = reservationsQuery.Where(_ => false);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim();
            reservationsQuery = reservationsQuery.Where(reservation =>
                (reservation.ExternalReservationReference != null && EF.Functions.ILike(reservation.ExternalReservationReference, $"%{searchTerm}%"))
                || (reservation.ConfirmationNumber != null && EF.Functions.ILike(reservation.ConfirmationNumber, $"%{searchTerm}%"))
                || EF.Functions.ILike(reservation.ReservationSource, $"%{searchTerm}%"));
        }

        var totalCount = await reservationsQuery.CountAsync(cancellationToken);
        var items = await reservationsQuery
            .OrderBy(reservation => reservation.CheckInDate)
            .ThenBy(reservation => reservation.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Reservation>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<Reservation?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Reservations
            .FirstOrDefaultAsync(reservation => reservation.Id == id && reservation.CompanyId == companyId, cancellationToken);
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Companies.AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken);
    }

    public Task<bool> PropertyBelongsToCompanyAsync(Guid propertyId, Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Properties.AnyAsync(property => property.Id == propertyId && property.CompanyId == companyId && property.IsActive, cancellationToken);
    }

    public Task<bool> GuestBelongsToCompanyAsync(Guid guestId, Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.Guests.AnyAsync(guest => guest.Id == guestId && guest.CompanyId == companyId && guest.IsActive, cancellationToken);
    }

    public async Task AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        await dbContext.Reservations.AddAsync(reservation, cancellationToken);
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
