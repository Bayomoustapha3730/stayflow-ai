using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IActionNotificationOutboxRepository
{
    Task<IReadOnlyCollection<ActionNotificationOutbox>> GetDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken);
    Task<PendingConciergeAction?> GetPendingActionAsync(Guid companyId, Guid actionId, CancellationToken cancellationToken);
    void MarkSent(ActionNotificationOutbox outbox, DateTimeOffset sentAtUtc);
    void MarkRetry(ActionNotificationOutbox outbox, string failureCode, DateTimeOffset nextAttemptAtUtc);
    void MarkTerminalFailure(ActionNotificationOutbox outbox, string failureCode);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
