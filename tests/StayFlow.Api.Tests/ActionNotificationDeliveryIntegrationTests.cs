using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Tests;

/// <summary>
/// Exercises ActionNotificationDeliveryProcessor against the real ConversationService,
/// ConversationChannelDispatcher, WhatsAppConversationChannelSender, customer-service-window
/// evaluator, and DevelopmentWhatsAppCloudClient/WhatsAppDevelopmentMessageStore - only the
/// EF-backed conversation repository is faked. No real Meta network call occurs.
/// </summary>
public sealed class ActionNotificationDeliveryIntegrationTests
{
    [Fact]
    public async Task ProcessDueAsync_OpenServiceWindow_DeliversViaDevelopmentClientAndMarksSent()
    {
        var fixture = new Fixture();
        fixture.AddInboundGuestMessage(DateTimeOffset.UtcNow.AddMinutes(-5));
        var outbox = fixture.QueueHostApprovedNotification();

        var result = await fixture.Processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Sent);
        Assert.Equal(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.NotNull(outbox.SentAt);
        Assert.Single(fixture.MessageStore.GetRecords());
        Assert.Equal(1, fixture.Coordinator.InvocationCount);
        Assert.StartsWith("whatsapp:message:", fixture.Coordinator.OperationKeys.Single(), StringComparison.Ordinal);
        Assert.Contains("late checkout", fixture.MessageStore.GetRecords().Single().BodyPreview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessDueAsync_ClosedServiceWindow_DoesNotMarkSentAndRetainsFailureReason()
    {
        var fixture = new Fixture();
        // No qualifying inbound guest message => customer-service window is closed.
        var outbox = fixture.QueueHostApprovedNotification();

        var result = await fixture.Processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(0, result.Sent);
        Assert.Equal(1, result.Failed);
        Assert.NotEqual(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.Null(outbox.SentAt);
        Assert.Equal("CustomerServiceWindowClosed", outbox.LastFailureCode);
        Assert.Empty(fixture.MessageStore.GetRecords());
    }

    [Fact]
    public async Task ProcessDueAsync_ReprocessingAlreadySentOutbox_DoesNotDuplicateGuestMessage()
    {
        var fixture = new Fixture();
        fixture.AddInboundGuestMessage(DateTimeOffset.UtcNow.AddMinutes(-5));
        var outbox = fixture.QueueHostApprovedNotification();

        await fixture.Processor.ProcessDueAsync(CancellationToken.None);
        Assert.Equal(ActionNotificationOutboxStatus.Sent, outbox.Status);
        var firstAttemptCount = outbox.AttemptCount;
        Assert.Single(fixture.MessageStore.GetRecords());

        var replay = await fixture.Processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(0, replay.Claimed);
        Assert.Equal(0, replay.Sent);
        Assert.Equal(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.Equal(firstAttemptCount, outbox.AttemptCount);

        // Seeded inbound guest message must be untouched by the outbound notification flow.
        Assert.Contains(fixture.ConversationRepository.Messages, m => m.SenderType == ConversationSenderType.Guest && m.Content == "When can I check out?");

        var approvalMessages = fixture.ConversationRepository.Messages
            .Where(m => m.SenderType == ConversationSenderType.System && m.Content.Contains("approved", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(approvalMessages);
        Assert.Single(fixture.MessageStore.GetRecords());
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Integration = new WhatsAppIntegration
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                DisplayName = "Demo",
                PhoneNumberId = "demo-phone-number-id",
                WhatsAppBusinessAccountId = "demo-waba-id",
                BusinessPhoneNumberMasked = "+1******1234",
                IsActive = true,
                IsProductionEnabled = false
            };

            Conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                GuestId = Guid.NewGuid(),
                Channel = GuestChannel.WhatsApp,
                ChannelIdentity = "+14155551234",
                WhatsAppIntegrationId = Integration.Id,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                LastActivityAt = DateTimeOffset.UtcNow
            };

            ConversationRepository = new FakeConversationRepository(Conversation);
            WhatsAppRepository = new FakeWhatsAppRepository(ConversationRepository.Messages, Integration);
            MessageStore = new WhatsAppDevelopmentMessageStore();

            var whatsAppOptions = Options.Create(new WhatsAppCloudOptions
            {
                DevelopmentMode = true,
                CustomerServiceWindowHours = 24
            });

            var whatsAppSender = new WhatsAppConversationChannelSender(
                new DevelopmentWhatsAppCloudClient(MessageStore, new PhoneNumberNormalizer()),
                WhatsAppRepository,
                new SuccessfulCredentialResolver(),
                new WhatsAppCustomerServiceWindowEvaluator(WhatsAppRepository, whatsAppOptions),
                new WhatsAppOutboundSendGate(whatsAppOptions),
                new PhoneNumberNormalizer(),
                NullLogger<WhatsAppConversationChannelSender>.Instance,
                Coordinator);

            var dispatcher = new ConversationChannelDispatcher(
                [whatsAppSender, new WebConversationChannelSender()],
                ConversationRepository,
                new NoOpConversationRealtimePublisher());

            ConversationService = new ConversationService(
                ConversationRepository,
                new FakeCurrentTenantContext(CompanyId),
                new ConversationStatusTransitionPolicy(),
                new NoOpConversationRealtimePublisher(),
                dispatcher,
                Options.Create(new ConversationOptions { MaxMessageCharacters = 2000, ReuseOpenConversationMinutes = 120, MaxHistoryMessages = 100 }),
                new ReservationLifecycleService(TimeProvider.System, Options.Create(new ReservationContextOptions())),
                WhatsAppRepository);

            var pendingAction = new PendingConciergeAction
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                ConversationId = Conversation.Id,
                PropertyId = Guid.NewGuid(),
                ActionType = ConciergeActionType.RequestLateCheckout,
                Status = PendingConciergeActionStatus.Completed,
                CreatedFromMessageId = Guid.NewGuid()
            };
            OutboxRepository = new FakeOutboxRepository(pendingAction);

            Processor = new ActionNotificationDeliveryProcessor(
                OutboxRepository,
                ConversationService,
                TimeProvider.System,
                Options.Create(new ActionNotificationDeliveryOptions { WorkerEnabled = false, BatchSize = 25, MaxAttempts = 5, RetryDelayMinutes = 5 }),
                NullLogger<ActionNotificationDeliveryProcessor>.Instance);
        }

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Conversation Conversation { get; }
        public WhatsAppIntegration Integration { get; }
        public FakeConversationRepository ConversationRepository { get; }
        public FakeWhatsAppRepository WhatsAppRepository { get; }
        public WhatsAppDevelopmentMessageStore MessageStore { get; }
        public ConversationService ConversationService { get; }
        public FakeOutboxRepository OutboxRepository { get; }
        public ActionNotificationDeliveryProcessor Processor { get; }
        public RecordingWhatsAppOutboundSendCoordinator Coordinator { get; } = new();

        public void AddInboundGuestMessage(DateTimeOffset sentAt)
        {
            ConversationRepository.Messages.Add(new ConversationMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                ConversationId = Conversation.Id,
                SenderType = ConversationSenderType.Guest,
                MessageType = ConversationMessageType.Text,
                Content = "When can I check out?",
                Provider = ConversationMessageProvider.WhatsAppCloud,
                SentAt = sentAt.ToUniversalTime()
            });
        }

        public ActionNotificationOutbox QueueHostApprovedNotification()
        {
            var outbox = new ActionNotificationOutbox
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                ActionId = OutboxRepository.PendingAction.Id,
                NotificationType = "HostApproved",
                PayloadReference = $"action:{OutboxRepository.PendingAction.Id:N}",
                Status = ActionNotificationOutboxStatus.Pending,
                NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            };
            OutboxRepository.Outbox.Add(outbox);
            return outbox;
        }
    }

    private sealed class FakeOutboxRepository(PendingConciergeAction pendingAction) : IActionNotificationOutboxRepository
    {
        public List<ActionNotificationOutbox> Outbox { get; } = [];
        public PendingConciergeAction PendingAction { get; } = pendingAction;

        public Task<IReadOnlyCollection<ActionNotificationOutbox>> GetDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ActionNotificationOutbox> due = Outbox
                .Where(item => item.Status == ActionNotificationOutboxStatus.Pending && item.NextAttemptAt <= nowUtc)
                .Take(batchSize)
                .ToList();
            return Task.FromResult(due);
        }

        public Task<PendingConciergeAction?> GetPendingActionAsync(Guid companyId, Guid actionId, CancellationToken cancellationToken)
        {
            var match = PendingAction.CompanyId == companyId && PendingAction.Id == actionId ? PendingAction : null;
            return Task.FromResult(match);
        }

        public void MarkSent(ActionNotificationOutbox item, DateTimeOffset sentAtUtc)
        {
            item.Status = ActionNotificationOutboxStatus.Sent;
            item.AttemptCount += 1;
            item.SentAt = sentAtUtc;
            item.LastFailureCode = null;
        }

        public void MarkRetry(ActionNotificationOutbox item, string failureCode, DateTimeOffset nextAttemptAtUtc)
        {
            item.AttemptCount += 1;
            item.NextAttemptAt = nextAttemptAtUtc;
            item.LastFailureCode = failureCode;
        }

        public void MarkTerminalFailure(ActionNotificationOutbox item, string failureCode)
        {
            item.Status = ActionNotificationOutboxStatus.Failed;
            item.AttemptCount += 1;
            item.LastFailureCode = failureCode;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCurrentTenantContext(Guid companyId) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = "action-notification-test";
        public bool IsAuthenticated { get; } = true;
    }

    private sealed class RecordingWhatsAppOutboundSendCoordinator : IWhatsAppOutboundSendCoordinator
    {
        public List<string> OperationKeys { get; } = [];
        public int InvocationCount { get; private set; }

        public Task<T> ExecuteAsync<T>(Guid companyId, string operationKey, Func<CancellationToken, Task<T>> send, CancellationToken cancellationToken)
        {
            InvocationCount++;
            OperationKeys.Add(operationKey);
            return send(cancellationToken);
        }
    }

    private sealed class SuccessfulCredentialResolver : IWhatsAppCredentialResolver
    {
        public Task<WhatsAppCredentialResolution> ResolveAsync(WhatsAppIntegration integration, CancellationToken cancellationToken)
            => Task.FromResult(new WhatsAppCredentialResolution { Success = true, AccessToken = "local-test-token" });
    }

    private sealed class NoOpConversationRealtimePublisher : IConversationRealtimePublisher
    {
        public Task PublishMessageCreatedAsync(Guid companyId, Guid conversationId, object payload, bool internalOnly, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishMessageUpdatedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishTypingStartedAsync(Guid companyId, Guid conversationId, object payload, bool hostOnly, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishTypingStoppedAsync(Guid companyId, Guid conversationId, object payload, bool hostOnly, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishConversationAssignedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishConversationStateChangedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishConversationReadStateChangedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishConversationUnreadCountChangedAsync(Guid companyId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishMessageDeliveryUpdatedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishHostCopilotWorkspaceUpdatedAsync(Guid companyId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
    }


    // Mirrors the production EF predicate in WhatsAppRepository.GetLatestInboundGuestWhatsAppMessageAsync.
    private sealed class FakeWhatsAppRepository(List<ConversationMessage> messages, WhatsAppIntegration integration) : IWhatsAppRepository
    {
        public Task<ConversationMessage?> GetLatestInboundGuestWhatsAppMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
        {
            var match = messages
                .Where(message => message.CompanyId == companyId
                    && message.ConversationId == conversationId
                    && !message.IsDeleted
                    && message.Provider == ConversationMessageProvider.WhatsAppCloud
                    && message.SenderType == ConversationSenderType.Guest)
                .OrderByDescending(message => message.SentAt)
                .FirstOrDefault();
            return Task.FromResult(match);
        }

        public Task<WhatsAppIntegration?> GetIntegrationForCompanyAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken)
            => Task.FromResult(integration.CompanyId == companyId && integration.Id == integrationId ? integration : null);

        public Task<IReadOnlyCollection<WhatsAppIntegration>> ListActiveIntegrationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppIntegration?> GetActiveIntegrationByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppIntegration?> GetSoleActiveIntegrationForCompanyAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<WhatsAppIntegration>> ListIntegrationsForCompanyAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddIntegrationAsync(WhatsAppIntegration newIntegration, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResult<WhatsAppTemplate>> ListTemplatesAsync(Guid companyId, Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppTemplate?> GetTemplateForCompanyAsync(Guid companyId, Guid integrationId, Guid templateId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppTemplate?> GetTemplateForCompanyAsync(Guid companyId, Guid templateId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppTemplate?> GetTemplateByNameAsync(Guid companyId, Guid integrationId, string name, string languageCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<WhatsAppTemplate>> ListTemplatesForIntegrationAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guest>> ListActiveGuestsWithPhoneAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsForGuestAsync(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConversationMessage?> FindMessageByProviderExternalIdAsync(Guid companyId, ConversationMessageProvider provider, string externalMessageId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddTemplateAsync(WhatsAppTemplate template, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    // Only the members actually exercised by AddLifecycleAutomationMessageAsync are implemented.
    private sealed class FakeConversationRepository(Conversation conversation) : IConversationRepository
    {
        public List<ConversationMessage> Messages { get; } = [];

        public Task<Conversation?> GetByIdForCompanyAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult(conversation.CompanyId == companyId && conversation.Id == conversationId ? conversation : null);

        public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, ConversationMessageProvider? provider, CancellationToken cancellationToken)
        {
            var match = Messages.FirstOrDefault(message => message.CompanyId == companyId
                && message.ExternalMessageId == externalMessageId
                && (provider is null || message.Provider == provider));
            return Task.FromResult(match);
        }

        public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<PagedResult<ConversationSummaryResponse>> ListConversationsAsync(Guid companyId, ConversationListQueryParameters query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetTotalUnreadCountForHostAsync(Guid companyId, Guid hostUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Dictionary<Guid, int>> GetUnreadMessageCountsForHostAsync(Guid companyId, Guid hostUserId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetUnreadHostMessageCountForGuestAsync(Guid companyId, Guid guestId, Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConversationMessage?> GetMessageForConversationAsync(Guid companyId, Guid conversationId, Guid messageId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, Guid? reservationId, Guid? propertyId, DateTimeOffset cutoff, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid companyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConversationMessage?> GetLatestVisibleMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConversationParticipantReadState?> GetReadStateAsync(Guid companyId, Guid conversationId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<ConversationParticipantReadState>> GetReadStatesForParticipantAsync(Guid companyId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddConversationAsync(Conversation newConversation, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddReadStateAsync(ConversationParticipantReadState state, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
