using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class ReservationLifecycleEventRepository(ApplicationDbContext dbContext) : IReservationLifecycleEventRepository
{
    private static readonly ReservationStatus[] EligibleReservationStatuses =
    [
        ReservationStatus.Confirmed,
        ReservationStatus.PreArrival,
        ReservationStatus.ReadyForCheckIn,
        ReservationStatus.CheckedIn,
        ReservationStatus.ActiveStay,
        ReservationStatus.CheckOutPending,
        ReservationStatus.CheckedOut,
        ReservationStatus.PostStay
    ];

    public Task<ReservationLifecycleEvent?> GetByIdempotencyKeyAsync(Guid companyId, string idempotencyKey, CancellationToken cancellationToken)
    {
        return dbContext.ReservationLifecycleEvents
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public Task<ReservationLifecycleEvent?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken)
    {
        return dbContext.ReservationLifecycleEvents
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ReservationLifecycleEvent>> GetPendingAsync(Guid companyId, DateTimeOffset dueBeforeUtc, int limit, CancellationToken cancellationToken)
    {
        return await dbContext.ReservationLifecycleEvents
            .Where(item => item.CompanyId == companyId
                && item.Status == ReservationLifecycleEventStatus.Pending
                && item.ScheduledForUtc <= dueBeforeUtc)
            .OrderBy(item => item.ScheduledForUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Reservation>> GetGenerationCandidatesAsync(DateOnly windowStart, DateOnly windowEnd, int limit, CancellationToken cancellationToken)
    {
        return await dbContext.Reservations
            .Include(reservation => reservation.Property)
            .Where(reservation => !reservation.IsDeleted
                && reservation.IsActive
                && EligibleReservationStatuses.Contains(reservation.Status)
                && reservation.CheckInDate <= windowEnd
                && reservation.CheckOutDate >= windowStart)
            .OrderBy(reservation => reservation.CheckInDate)
            .ThenBy(reservation => reservation.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ReservationLifecycleEvent>> ClaimDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var claimed = await dbContext.ReservationLifecycleEvents
            .FromSqlInterpolated($"""
                SELECT *
                FROM "ReservationLifecycleEvents"
                WHERE "Status" = 'Pending'
                  AND "ScheduledForUtc" <= {nowUtc}
                ORDER BY "ScheduledForUtc", "CreatedAt", "Id"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var lifecycleEvent in claimed)
        {
            lifecycleEvent.Status = ReservationLifecycleEventStatus.Processing;
            lifecycleEvent.AttemptCount += 1;
            lifecycleEvent.LastAttemptAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return claimed;
    }

    public Task<int> RecoverStaleProcessingAsync(DateTimeOffset staleBeforeUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        return dbContext.ReservationLifecycleEvents
            .Where(item => item.Status == ReservationLifecycleEventStatus.Processing
                && item.LastAttemptAtUtc != null
                && item.LastAttemptAtUtc < staleBeforeUtc)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(item => item.Status, ReservationLifecycleEventStatus.Pending)
                .SetProperty(item => item.UpdatedAt, nowUtc),
                cancellationToken);
    }

    public Task<int> RecoverRetryableFailedAsync(DateTimeOffset retryBeforeUtc, DateTimeOffset nowUtc, int maxAttempts, CancellationToken cancellationToken)
    {
        return dbContext.ReservationLifecycleEvents
            .Where(item => item.Status == ReservationLifecycleEventStatus.Failed
                && item.AttemptCount < maxAttempts
                && item.LastAttemptAtUtc != null
                && item.LastAttemptAtUtc <= retryBeforeUtc)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(item => item.Status, ReservationLifecycleEventStatus.Pending)
                .SetProperty(item => item.UpdatedAt, nowUtc),
                cancellationToken);
    }

    public Task<Reservation?> GetReservationForEventAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
    {
        return dbContext.Reservations
            .FirstOrDefaultAsync(reservation => reservation.CompanyId == lifecycleEvent.CompanyId
                && reservation.Id == lifecycleEvent.ReservationId,
                cancellationToken);
    }

    public async Task<int> SuppressObsoleteUnprocessedAsync(Guid companyId, Guid reservationId, IReadOnlyCollection<string> currentIdempotencyKeys, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken)
    {
        var obsoleteEvents = await dbContext.ReservationLifecycleEvents
            .Where(item => item.CompanyId == companyId
                && item.ReservationId == reservationId
                && (item.Status == ReservationLifecycleEventStatus.Pending || item.Status == ReservationLifecycleEventStatus.Failed)
                && !currentIdempotencyKeys.Contains(item.IdempotencyKey))
            .ToListAsync(cancellationToken);

        foreach (var lifecycleEvent in obsoleteEvents)
        {
            lifecycleEvent.Status = ReservationLifecycleEventStatus.Suppressed;
            lifecycleEvent.ProcessedAtUtc = nowUtc;
            lifecycleEvent.LastError = reason.Length > 500 ? reason[..500] : reason;
        }

        if (obsoleteEvents.Count == 0)
        {
            return 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return obsoleteEvents.Count;
    }

    public async Task AddAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
    {
        await dbContext.ReservationLifecycleEvents.AddAsync(lifecycleEvent, cancellationToken);
    }

    public void Detach(ReservationLifecycleEvent lifecycleEvent)
    {
        dbContext.Entry(lifecycleEvent).State = EntityState.Detached;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

}
