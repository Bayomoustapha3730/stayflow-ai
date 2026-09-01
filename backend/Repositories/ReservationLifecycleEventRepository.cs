using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class ReservationLifecycleEventRepository(ApplicationDbContext dbContext) : IReservationLifecycleEventRepository
{
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
