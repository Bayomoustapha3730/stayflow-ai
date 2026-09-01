using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IReservationLifecycleEventRepository
{
    Task<ReservationLifecycleEvent?> GetByIdempotencyKeyAsync(Guid companyId, string idempotencyKey, CancellationToken cancellationToken);
    Task<ReservationLifecycleEvent?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ReservationLifecycleEvent>> GetPendingAsync(Guid companyId, DateTimeOffset dueBeforeUtc, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Reservation>> GetGenerationCandidatesAsync(DateOnly windowStart, DateOnly windowEnd, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ReservationLifecycleEvent>> ClaimDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken);
    Task<int> RecoverStaleProcessingAsync(DateTimeOffset staleBeforeUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task<int> RecoverRetryableFailedAsync(DateTimeOffset retryBeforeUtc, DateTimeOffset nowUtc, int maxAttempts, CancellationToken cancellationToken);
    Task<Reservation?> GetReservationForEventAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken);
    Task<int> SuppressObsoleteUnprocessedAsync(Guid companyId, Guid reservationId, IReadOnlyCollection<string> currentIdempotencyKeys, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken);
    Task AddAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken);
    // Removes a failed insert attempt from the change tracker so the same DbContext
    // instance can continue to be used for subsequent, unrelated saves.
    void Detach(ReservationLifecycleEvent lifecycleEvent);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
