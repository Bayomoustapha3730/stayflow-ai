using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IReservationLifecycleEventRepository
{
    Task<ReservationLifecycleEvent?> GetByIdempotencyKeyAsync(Guid companyId, string idempotencyKey, CancellationToken cancellationToken);
    Task<ReservationLifecycleEvent?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ReservationLifecycleEvent>> GetPendingAsync(Guid companyId, DateTimeOffset dueBeforeUtc, int limit, CancellationToken cancellationToken);
    Task AddAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken);
    // Removes a failed insert attempt from the change tracker so the same DbContext
    // instance can continue to be used for subsequent, unrelated saves.
    void Detach(ReservationLifecycleEvent lifecycleEvent);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
