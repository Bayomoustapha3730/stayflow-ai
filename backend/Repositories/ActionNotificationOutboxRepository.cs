using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class ActionNotificationOutboxRepository(ApplicationDbContext dbContext) : IActionNotificationOutboxRepository
{
    public Task<IReadOnlyCollection<ActionNotificationOutbox>> GetDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken)
    {
        return GetDueCoreAsync(nowUtc, batchSize, cancellationToken);
    }

    private async Task<IReadOnlyCollection<ActionNotificationOutbox>> GetDueCoreAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken)
    {
        var due = await dbContext.ActionNotificationOutbox
            .Where(item => item.Status == ActionNotificationOutboxStatus.Pending && item.NextAttemptAt <= nowUtc)
            .OrderBy(item => item.NextAttemptAt)
            .ThenBy(item => item.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return due;
    }

    public Task<PendingConciergeAction?> GetPendingActionAsync(Guid companyId, Guid actionId, CancellationToken cancellationToken)
    {
        return dbContext.PendingConciergeActions
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Id == actionId, cancellationToken);
    }

    public void MarkSent(ActionNotificationOutbox outbox, DateTimeOffset sentAtUtc)
    {
        outbox.Status = ActionNotificationOutboxStatus.Sent;
        outbox.AttemptCount += 1;
        outbox.SentAt = sentAtUtc;
        outbox.LastFailureCode = null;
    }

    public void MarkRetry(ActionNotificationOutbox outbox, string failureCode, DateTimeOffset nextAttemptAtUtc)
    {
        outbox.AttemptCount += 1;
        outbox.NextAttemptAt = nextAttemptAtUtc;
        outbox.LastFailureCode = failureCode;
    }

    public void MarkTerminalFailure(ActionNotificationOutbox outbox, string failureCode)
    {
        outbox.Status = ActionNotificationOutboxStatus.Failed;
        outbox.AttemptCount += 1;
        outbox.LastFailureCode = failureCode;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
