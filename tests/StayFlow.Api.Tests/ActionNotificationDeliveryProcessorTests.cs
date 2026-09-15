using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Tests;

public sealed class ActionNotificationDeliveryProcessorTests
{
    [Fact]
    public async Task ProcessDueAsync_HostApproved_SendsGuestMessageAndMarksSent()
    {
        var pendingAction = NewPendingAction();
        var outbox = NewOutbox(pendingAction, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Sent
        }));

        var processor = CreateProcessor(repository, conversationService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Claimed);
        Assert.Equal(1, result.Sent);
        Assert.Equal(0, result.Failed);
        Assert.Equal(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.NotNull(outbox.SentAt);
        Assert.Equal(1, conversationService.CallCount);
        Assert.Contains("approved", conversationService.LastContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessDueAsync_OpenWindowFreeFormSuccess_DoesNotInvokeTemplateFallback()
    {
        var pendingAction = NewPendingAction(ConciergeActionType.RequestLateCheckout);
        var outbox = NewOutbox(pendingAction, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Sent
        }));
        var templateService = new FakeWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Sent
        }));

        var processor = CreateProcessor(repository, conversationService, templateService: templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Sent);
        Assert.Equal(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.Equal(1, conversationService.CallCount);
        Assert.EndsWith(":freeform", conversationService.LastIdempotencyKey, StringComparison.Ordinal);
        Assert.Equal(0, templateService.CallCount);
    }

    [Fact]
    public async Task ProcessDueAsync_ClosedWindowApprovedTemplateSuccess_MarksSentWithTemplateKey()
    {
        var pendingAction = NewPendingAction(ConciergeActionType.RequestLateCheckout);
        var outbox = NewOutbox(pendingAction, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = ClosedWindowConversationService();
        var templateService = new FakeWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Sent
        }));

        var processor = CreateProcessor(repository, conversationService, templateService: templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Sent);
        Assert.Equal(0, result.Failed);
        Assert.Equal(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.EndsWith(":freeform", conversationService.LastIdempotencyKey, StringComparison.Ordinal);
        Assert.Equal(1, templateService.CallCount);
        Assert.Equal(ConciergeActionType.RequestLateCheckout, templateService.LastActionType);
        Assert.Equal("HostApproved", templateService.LastNotificationType);
        Assert.EndsWith(":template", templateService.LastIdempotencyKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessDueAsync_ClosedWindowDeclinedTemplateSuccess_MarksSentWithTemplateKey()
    {
        var pendingAction = NewPendingAction(ConciergeActionType.RequestLateCheckout);
        var outbox = NewOutbox(pendingAction, "HostDeclined");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = ClosedWindowConversationService();
        var templateService = new FakeWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Sent
        }));

        var processor = CreateProcessor(repository, conversationService, templateService: templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Sent);
        Assert.Equal(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.Equal(1, templateService.CallCount);
        Assert.Equal("HostDeclined", templateService.LastNotificationType);
        Assert.EndsWith(":template", templateService.LastIdempotencyKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessDueAsync_ClosedWindowTemplateUnavailable_DoesNotMarkSentAndRetries()
    {
        var pendingAction = NewPendingAction(ConciergeActionType.RequestLateCheckout);
        var outbox = NewOutbox(pendingAction, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = ClosedWindowConversationService();
        var templateService = new FakeWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Fail("No approved host action template is configured."));

        var processor = CreateProcessor(repository, conversationService, maxAttempts: 5, templateService: templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(0, result.Sent);
        Assert.Equal(1, result.Failed);
        Assert.Equal(ActionNotificationOutboxStatus.Pending, outbox.Status);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.Equal("No approved host action template is configured.", outbox.LastFailureCode);
        Assert.Equal(1, templateService.CallCount);
    }

    [Fact]
    public async Task ProcessDueAsync_NonWindowFreeFormFailure_DoesNotInvokeTemplateFallback()
    {
        var pendingAction = NewPendingAction(ConciergeActionType.RequestLateCheckout);
        var outbox = NewOutbox(pendingAction, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Failed,
            FailureCode = "ProviderUnavailable"
        }));
        var templateService = new FakeWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Sent
        }));

        var processor = CreateProcessor(repository, conversationService, maxAttempts: 5, templateService: templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(0, result.Sent);
        Assert.Equal(1, result.Failed);
        Assert.Equal(ActionNotificationOutboxStatus.Pending, outbox.Status);
        Assert.Equal("ProviderUnavailable", outbox.LastFailureCode);
        Assert.Equal(0, templateService.CallCount);
    }

    [Fact]
    public async Task ProcessDueAsync_ReprocessingSentTemplateFallback_DoesNotDuplicateTemplateDelivery()
    {
        var pendingAction = NewPendingAction(ConciergeActionType.RequestLateCheckout);
        var outbox = NewOutbox(pendingAction, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = ClosedWindowConversationService();
        var templateService = new FakeWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Sent
        }));

        var processor = CreateProcessor(repository, conversationService, templateService: templateService);
        await processor.ProcessDueAsync(CancellationToken.None);
        var firstAttemptCount = outbox.AttemptCount;
        var replay = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.Equal(firstAttemptCount, outbox.AttemptCount);
        Assert.Equal(0, replay.Claimed);
        Assert.Equal(1, conversationService.CallCount);
        Assert.Equal(1, templateService.CallCount);
    }

    [Fact]
    public async Task ProcessDueAsync_MessageBeforeNextAttemptAt_IsNotProcessed()
    {
        var pendingAction = NewPendingAction();
        var outbox = NewOutbox(pendingAction, "HostApproved");
        outbox.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Sent
        }));

        var processor = CreateProcessor(repository, conversationService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(0, result.Claimed);
        Assert.Equal(ActionNotificationOutboxStatus.Pending, outbox.Status);
        Assert.Equal(0, conversationService.CallCount);
    }

    [Fact]
    public async Task ProcessDueAsync_ClosedWindowTemplateFailureAtMaxAttempts_MarksTerminalFailure()
    {
        var pendingAction = NewPendingAction(ConciergeActionType.RequestLateCheckout);
        var outbox = NewOutbox(pendingAction, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = ClosedWindowConversationService();
        var templateService = new FakeWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Fail("template rejected"));

        var processor = CreateProcessor(repository, conversationService, maxAttempts: 1, templateService: templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(ActionNotificationOutboxStatus.Failed, outbox.Status);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.Equal("template rejected", outbox.LastFailureCode);
        Assert.Equal(1, templateService.CallCount);
    }

    [Fact]
    public async Task ProcessDueAsync_HostDeclined_SendsGuestDeclineMessage()
    {
        var pendingAction = NewPendingAction();
        var outbox = NewOutbox(pendingAction, "HostDeclined");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Sent
        }));

        var processor = CreateProcessor(repository, conversationService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Sent);
        Assert.Equal(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.Contains("sorry", conversationService.LastContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessDueAsync_SubmissionNotificationType_MarksSentWithoutSendingMessage()
    {
        var pendingAction = NewPendingAction();
        var outbox = NewOutbox(pendingAction, nameof(ConciergeActionType.RequestEarlyCheckIn));
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse()));

        var processor = CreateProcessor(repository, conversationService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.Equal(0, conversationService.CallCount);
    }

    [Fact]
    public async Task ProcessDueAsync_MessagePersistedButWhatsAppDeliveryFailed_DoesNotMarkSent()
    {
        // Simulates ConversationService.AddLifecycleAutomationMessageAsync returning Success=true
        // (message stored) while the underlying WhatsApp send failed, e.g. a closed customer-service window.
        var pendingAction = NewPendingAction();
        var outbox = NewOutbox(pendingAction, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Failed,
            SafeFailureSummary = "CustomerServiceWindowClosed"
        }));

        var processor = CreateProcessor(repository, conversationService, maxAttempts: 5);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(0, result.Sent);
        Assert.NotEqual(ActionNotificationOutboxStatus.Sent, outbox.Status);
        Assert.Equal("CustomerServiceWindowClosed", outbox.LastFailureCode);
    }

    [Fact]
    public async Task ProcessDueAsync_MissingPendingAction_MarksTerminalFailure()
    {
        var outbox = NewOutbox(pendingAction: null, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction: null);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse()));

        var processor = CreateProcessor(repository, conversationService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(ActionNotificationOutboxStatus.Failed, outbox.Status);
        Assert.Equal("ActionNotFound", outbox.LastFailureCode);
    }

    [Fact]
    public async Task ProcessDueAsync_TransientSendFailure_BelowMaxAttempts_StaysPendingForRetry()
    {
        var pendingAction = NewPendingAction();
        var outbox = NewOutbox(pendingAction, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Fail("boom"));

        var processor = CreateProcessor(repository, conversationService, maxAttempts: 5);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(ActionNotificationOutboxStatus.Pending, outbox.Status);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.True(outbox.NextAttemptAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ProcessDueAsync_TransientSendFailure_ReachesMaxAttempts_MarksTerminalFailure()
    {
        var pendingAction = NewPendingAction();
        var outbox = NewOutbox(pendingAction, "HostApproved");
        var repository = new FakeRepository([outbox], pendingAction);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Fail("boom"));

        var processor = CreateProcessor(repository, conversationService, maxAttempts: 1);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(ActionNotificationOutboxStatus.Failed, outbox.Status);
        Assert.Equal(1, outbox.AttemptCount);
    }

    [Fact]
    public async Task ProcessDueAsync_TenantIsolation_OnlyUsesMatchingCompanyPendingAction()
    {
        var pendingAction = NewPendingAction();
        var outbox = NewOutbox(pendingAction, "HostApproved");
        outbox.CompanyId = Guid.NewGuid();
        var repository = new FakeRepository([outbox], pendingAction: null);
        var conversationService = new FakeConversationService(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse()));

        var processor = CreateProcessor(repository, conversationService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(0, conversationService.CallCount);
    }

    private static PendingConciergeAction NewPendingAction(ConciergeActionType actionType = ConciergeActionType.RequestEarlyCheckIn)
    {
        return new PendingConciergeAction
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            ActionType = actionType,
            Status = PendingConciergeActionStatus.Completed,
            CreatedFromMessageId = Guid.NewGuid()
        };
    }

    private static ActionNotificationOutbox NewOutbox(PendingConciergeAction? pendingAction, string notificationType)
    {
        return new ActionNotificationOutbox
        {
            Id = Guid.NewGuid(),
            CompanyId = pendingAction?.CompanyId ?? Guid.NewGuid(),
            ActionId = pendingAction?.Id ?? Guid.NewGuid(),
            NotificationType = notificationType,
            PayloadReference = "action:test",
            Status = ActionNotificationOutboxStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
    }

    private static ActionNotificationDeliveryProcessor CreateProcessor(
        FakeRepository repository,
        FakeConversationService conversationService,
        int maxAttempts = 5,
        FakeWhatsAppTemplateService? templateService = null)
    {
        var options = Options.Create(new ActionNotificationDeliveryOptions
        {
            WorkerEnabled = false,
            BatchSize = 25,
            MaxAttempts = maxAttempts,
            RetryDelayMinutes = 5
        });

        return new ActionNotificationDeliveryProcessor(
            repository,
            conversationService,
            TimeProvider.System,
            options,
            NullLogger<ActionNotificationDeliveryProcessor>.Instance,
            templateService);
    }

    private static FakeConversationService ClosedWindowConversationService()
        => new(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            DeliveryStatus = ConversationMessageDeliveryStatus.Failed,
            FailureCode = "CustomerServiceWindowClosed",
            SafeFailureSummary = "CustomerServiceWindowClosed"
        }));

    private sealed class FakeRepository(
        List<ActionNotificationOutbox> outbox,
        PendingConciergeAction? pendingAction) : IActionNotificationOutboxRepository
    {
        public Task<IReadOnlyCollection<ActionNotificationOutbox>> GetDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ActionNotificationOutbox> due = outbox
                .Where(item => item.Status == ActionNotificationOutboxStatus.Pending && item.NextAttemptAt <= nowUtc)
                .Take(batchSize)
                .ToList();

            return Task.FromResult(due);
        }

        public Task<PendingConciergeAction?> GetPendingActionAsync(Guid companyId, Guid actionId, CancellationToken cancellationToken)
        {
            var match = pendingAction is not null && pendingAction.CompanyId == companyId && pendingAction.Id == actionId
                ? pendingAction
                : null;

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

    private sealed class FakeConversationService(ApiResponse<ConversationMessageResponse> response) : IConversationService
    {
        public int CallCount { get; private set; }
        public string LastContent { get; private set; } = string.Empty;
        public string LastIdempotencyKey { get; private set; } = string.Empty;

        public Task<ApiResponse<ConversationMessageResponse>> AddLifecycleAutomationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken)
        {
            CallCount++;
            LastContent = content;
            LastIdempotencyKey = idempotencyKey;
            return Task.FromResult(response);
        }

        public Task<ApiResponse<ConversationDetailResponse>> CreateOrGetConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationListResponse>> GetConversationsAsync(ConversationListQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationHistoryResponse>> GetConversationHistoryAsync(Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddGuestMessageAsync(Guid conversationId, AddGuestMessageRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddAIMessageAsync(Guid conversationId, string content, AIOrchestrationResult result, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddHostMessageAsync(Guid conversationId, AddHostMessageRequest request, WhatsAppSendOrigin origin, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> RetryFailedMessageAsync(Guid conversationId, Guid messageId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddInternalNoteAsync(Guid conversationId, AddInternalNoteRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddPaymentConfirmationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> UpdateMessageDeliveryStatusAsync(Guid conversationId, Guid messageId, ConversationMessageDeliveryStatus status, DateTimeOffset occurredAt, string? failureCode, string? failureReason, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> EscalateConversationAsync(Guid conversationId, EscalateConversationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> EnableHumanTakeoverAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> ReturnToAIModeAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> ResolveConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> CloseConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> AssignConversationToCurrentUserAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> UnassignConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<bool>> MarkConversationReadForCurrentUserAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<bool>> MarkConversationReadForGuestAsync(Guid conversationId, Guid guestId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ChatMessageFeedbackResponse>> AddGuestMessageFeedbackAsync(Guid conversationId, Guid messageId, AddChatMessageFeedbackRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationFeedbackAnalyticsResponse>> GetFeedbackAnalyticsAsync(ConversationFeedbackAnalyticsQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class FakeWhatsAppTemplateService(ApiResponse<ConversationMessageResponse> response) : IWhatsAppTemplateService
    {
        public int CallCount { get; private set; }
        public ConciergeActionType? LastActionType { get; private set; }
        public string LastNotificationType { get; private set; } = string.Empty;
        public string LastIdempotencyKey { get; private set; } = string.Empty;

        public Task<ApiResponse<ConversationMessageResponse>> SendHostActionTemplateMessageAsync(Guid companyId, Guid conversationId, ConciergeActionType actionType, string notificationType, string idempotencyKey, CancellationToken cancellationToken)
        {
            CallCount++;
            LastActionType = actionType;
            LastNotificationType = notificationType;
            LastIdempotencyKey = idempotencyKey;
            return Task.FromResult(response);
        }

        public Task<ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>> GetIntegrationsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppIntegrationDetailResponse>> GetIntegrationDetailAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppIntegrationDetailResponse>> CreateIntegrationAsync(WhatsAppIntegrationConfigurationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppIntegrationDetailResponse>> UpdateIntegrationAsync(Guid integrationId, WhatsAppIntegrationConfigurationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppProductionEnableResponse>> EnableProductionAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppProductionEnableResponse>> DisableProductionAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppIntegrationHealthResponse>> CheckHealthAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplateSyncResponse>> SyncTemplatesAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplateListResponse>> ListTemplatesAsync(Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplateDetailResponse>> GetTemplateAsync(Guid integrationId, Guid templateId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplatePreviewResponse>> PreviewTemplateAsync(Guid integrationId, Guid templateId, WhatsAppTemplatePreviewRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> SendTemplateMessageAsync(Guid conversationId, Guid templateId, SendWhatsAppTemplateMessageRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> SendLifecycleAutomationTemplateMessageAsync(Guid companyId, Guid conversationId, Guid integrationId, Guid templateId, IReadOnlyCollection<string> variables, string idempotencyKey, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>> GetCustomerServiceWindowStatusAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
