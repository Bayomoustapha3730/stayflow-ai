using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class GuestJourneyDeliveryReceiptSynchronizerTests
{
    [Fact]
    public async Task DeliveredWebhook_UpdatesGuestJourneyMessageToDelivered()
    {
        var companyId = Guid.NewGuid();
        var conversationMessageId = Guid.NewGuid();
        var message = NewMessage(companyId, conversationMessageId);
        var repository = new FakeRepository(message);
        var synchronizer = new GuestJourneyDeliveryReceiptSynchronizer(repository, NullLogger<GuestJourneyDeliveryReceiptSynchronizer>.Instance);
        var occurredAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

        var changed = await synchronizer.SyncAsync(companyId, conversationMessageId, ConversationMessageDeliveryStatus.Delivered, occurredAt, null, null, CancellationToken.None);

        Assert.True(changed);
        Assert.Equal(GuestJourneyMessageStatus.Delivered, message.Status);
        Assert.Equal(occurredAt, message.DeliveredAtUtc);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task ReadWebhook_EnsuresGuestJourneyMessageDelivered()
    {
        var companyId = Guid.NewGuid();
        var conversationMessageId = Guid.NewGuid();
        var message = NewMessage(companyId, conversationMessageId);
        var repository = new FakeRepository(message);
        var synchronizer = new GuestJourneyDeliveryReceiptSynchronizer(repository, NullLogger<GuestJourneyDeliveryReceiptSynchronizer>.Instance);
        var occurredAt = new DateTimeOffset(2026, 8, 10, 12, 5, 0, TimeSpan.Zero);

        var changed = await synchronizer.SyncAsync(companyId, conversationMessageId, ConversationMessageDeliveryStatus.Read, occurredAt, null, null, CancellationToken.None);

        Assert.True(changed);
        Assert.Equal(GuestJourneyMessageStatus.Delivered, message.Status);
        Assert.Equal(occurredAt, message.DeliveredAtUtc);
    }

    [Fact]
    public async Task FailedWebhook_SynchronizesGuestJourneyMessageSafely()
    {
        var companyId = Guid.NewGuid();
        var conversationMessageId = Guid.NewGuid();
        var message = NewMessage(companyId, conversationMessageId);
        var repository = new FakeRepository(message);
        var synchronizer = new GuestJourneyDeliveryReceiptSynchronizer(repository, NullLogger<GuestJourneyDeliveryReceiptSynchronizer>.Instance);
        var occurredAt = new DateTimeOffset(2026, 8, 10, 12, 10, 0, TimeSpan.Zero);

        var changed = await synchronizer.SyncAsync(companyId, conversationMessageId, ConversationMessageDeliveryStatus.Failed, occurredAt, "131047", "Window closed", CancellationToken.None);

        Assert.True(changed);
        Assert.Equal(GuestJourneyMessageStatus.Blocked, message.Status);
        Assert.Equal(occurredAt, message.FailedAtUtc);
        Assert.Equal("131047: Window closed", message.LastError);
    }

    [Fact]
    public async Task SentWebhook_DoesNotMarkGuestJourneyMessageDelivered()
    {
        var companyId = Guid.NewGuid();
        var conversationMessageId = Guid.NewGuid();
        var message = NewMessage(companyId, conversationMessageId);
        var repository = new FakeRepository(message);
        var synchronizer = new GuestJourneyDeliveryReceiptSynchronizer(repository, NullLogger<GuestJourneyDeliveryReceiptSynchronizer>.Instance);

        var changed = await synchronizer.SyncAsync(companyId, conversationMessageId, ConversationMessageDeliveryStatus.Sent, DateTimeOffset.UtcNow, null, null, CancellationToken.None);

        Assert.False(changed);
        Assert.Equal(GuestJourneyMessageStatus.Accepted, message.Status);
        Assert.Null(message.DeliveredAtUtc);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task OrdinaryConversationMessageWebhook_DoesNotRequireGuestJourneyMessage()
    {
        var repository = new FakeRepository(null);
        var synchronizer = new GuestJourneyDeliveryReceiptSynchronizer(repository, NullLogger<GuestJourneyDeliveryReceiptSynchronizer>.Instance);

        var changed = await synchronizer.SyncAsync(Guid.NewGuid(), Guid.NewGuid(), ConversationMessageDeliveryStatus.Delivered, DateTimeOffset.UtcNow, null, null, CancellationToken.None);

        Assert.False(changed);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    private static GuestJourneyMessage NewMessage(Guid companyId, Guid conversationMessageId)
    {
        return new GuestJourneyMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ReservationId = Guid.NewGuid(),
            ReservationLifecycleEventId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            GuestId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            ConversationMessageId = conversationMessageId,
            JourneyEventType = ReservationLifecycleEventType.ArrivalDay,
            Language = "en",
            RenderedContent = "Hi Ada",
            Status = GuestJourneyMessageStatus.Accepted,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };
    }

    private sealed class FakeRepository(GuestJourneyMessage? message) : IGuestJourneyMessageRepository
    {
        public int SaveChangesCount { get; private set; }

        public Task<GuestJourneyMessage?> FindByConversationMessageAsync(Guid companyId, Guid conversationMessageId, CancellationToken cancellationToken)
        {
            return Task.FromResult(message is not null && message.CompanyId == companyId && message.ConversationMessageId == conversationMessageId ? message : null);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }

        public Task<ReservationLifecycleEvent?> GetLifecycleEventContextAsync(Guid companyId, Guid lifecycleEventId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<GuestJourneyMessage?> GetByLifecycleEventAsync(Guid companyId, Guid lifecycleEventId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Conversation?> GetLatestConversationForReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddAsync(GuestJourneyMessage message, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void Detach(GuestJourneyMessage message) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<GuestJourneyMessage>> ClaimDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> RecoverStaleProcessingAsync(DateTimeOffset staleBeforeUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> RecoverRetryableFailedAsync(DateTimeOffset nowUtc, int maxAttempts, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ReservationLifecycleEvent?> GetLifecycleEventForDeliveryAsync(GuestJourneyMessage message, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WhatsAppIntegration?> GetActiveWhatsAppIntegrationAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task MarkAcceptedAsync(GuestJourneyMessage message, Guid conversationMessageId, string? providerMessageId, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task MarkFailedAsync(GuestJourneyMessage message, string error, DateTimeOffset nextAttemptAtUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task MarkBlockedAsync(GuestJourneyMessage message, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task MarkSuppressedAsync(GuestJourneyMessage message, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
