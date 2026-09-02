using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class GuestJourneyMessageRepository(ApplicationDbContext dbContext) : IGuestJourneyMessageRepository
{
    public Task<ReservationLifecycleEvent?> GetLifecycleEventContextAsync(Guid companyId, Guid lifecycleEventId, CancellationToken cancellationToken)
    {
        return dbContext.ReservationLifecycleEvents
            .Include(item => item.Company)
            .Include(item => item.Reservation)
            .Include(item => item.Property)
            .Include(item => item.Guest)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Id == lifecycleEventId, cancellationToken);
    }

    public Task<GuestJourneyMessage?> GetByLifecycleEventAsync(Guid companyId, Guid lifecycleEventId, CancellationToken cancellationToken)
    {
        return dbContext.GuestJourneyMessages
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.ReservationLifecycleEventId == lifecycleEventId, cancellationToken);
    }

    public Task<Conversation?> GetLatestConversationForReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken)
    {
        return dbContext.Conversations
            .Where(conversation => conversation.CompanyId == companyId
                && conversation.ReservationId == reservationId
                && conversation.Channel == GuestChannel.WhatsApp
                && conversation.Status != ConversationStatus.Closed
                && !conversation.IsDeleted)
            .OrderByDescending(conversation => conversation.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        await dbContext.Conversations.AddAsync(conversation, cancellationToken);
    }

    public async Task AddAsync(GuestJourneyMessage message, CancellationToken cancellationToken)
    {
        await dbContext.GuestJourneyMessages.AddAsync(message, cancellationToken);
    }

    public void Detach(GuestJourneyMessage message)
    {
        dbContext.Entry(message).State = EntityState.Detached;
    }

    public async Task<IReadOnlyCollection<GuestJourneyMessage>> ClaimDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var claimed = await dbContext.GuestJourneyMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM "GuestJourneyMessages"
                WHERE "Status" = 'Pending'
                  AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= {nowUtc})
                ORDER BY COALESCE("NextAttemptAtUtc", "CreatedAt"), "CreatedAt", "Id"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in claimed)
        {
            message.Status = GuestJourneyMessageStatus.Processing;
            message.AttemptCount += 1;
            message.LastAttemptAtUtc = nowUtc;
            message.LastError = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    public Task<int> RecoverStaleProcessingAsync(DateTimeOffset staleBeforeUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        return dbContext.GuestJourneyMessages
            .Where(item => item.Status == GuestJourneyMessageStatus.Processing
                && item.LastAttemptAtUtc != null
                && item.LastAttemptAtUtc < staleBeforeUtc)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(item => item.Status, GuestJourneyMessageStatus.Pending)
                .SetProperty(item => item.UpdatedAt, nowUtc),
                cancellationToken);
    }

    public Task<int> RecoverRetryableFailedAsync(DateTimeOffset nowUtc, int maxAttempts, CancellationToken cancellationToken)
    {
        return dbContext.GuestJourneyMessages
            .Where(item => item.Status == GuestJourneyMessageStatus.Failed
                && item.AttemptCount < maxAttempts
                && (item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= nowUtc))
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(item => item.Status, GuestJourneyMessageStatus.Pending)
                .SetProperty(item => item.UpdatedAt, nowUtc),
                cancellationToken);
    }

    public Task<ReservationLifecycleEvent?> GetLifecycleEventForDeliveryAsync(GuestJourneyMessage message, CancellationToken cancellationToken)
    {
        return dbContext.ReservationLifecycleEvents
            .Include(item => item.Reservation)
            .Include(item => item.Property)
            .Include(item => item.Guest)
            .FirstOrDefaultAsync(item => item.CompanyId == message.CompanyId && item.Id == message.ReservationLifecycleEventId, cancellationToken);
    }

    public Task<GuestJourneyMessage?> FindByConversationMessageAsync(Guid companyId, Guid conversationMessageId, CancellationToken cancellationToken)
    {
        return dbContext.GuestJourneyMessages
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.ConversationMessageId == conversationMessageId, cancellationToken);
    }

    public Task<WhatsAppIntegration?> GetActiveWhatsAppIntegrationAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.WhatsAppIntegrations
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.IsActive, cancellationToken);
    }

    public Task MarkAcceptedAsync(GuestJourneyMessage message, Guid conversationMessageId, string? providerMessageId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        message.Status = GuestJourneyMessageStatus.Accepted;
        message.ConversationMessageId = conversationMessageId;
        message.ProviderMessageId = providerMessageId;
        message.AcceptedAtUtc = nowUtc;
        message.FailedAtUtc = null;
        message.LastError = null;
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task MarkFailedAsync(GuestJourneyMessage message, string error, DateTimeOffset nextAttemptAtUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        message.Status = GuestJourneyMessageStatus.Failed;
        message.FailedAtUtc = nowUtc;
        message.NextAttemptAtUtc = nextAttemptAtUtc;
        message.LastError = Truncate(error);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task MarkBlockedAsync(GuestJourneyMessage message, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        message.Status = GuestJourneyMessageStatus.Blocked;
        message.FailedAtUtc = nowUtc;
        message.LastError = Truncate(reason);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task MarkSuppressedAsync(GuestJourneyMessage message, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        message.Status = GuestJourneyMessageStatus.Suppressed;
        message.FailedAtUtc = nowUtc;
        message.LastError = Truncate(reason);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value) => value.Length > 500 ? value[..500] : value;
}