using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppTemplateServiceOriginTests
{
    [Fact]
    public async Task SendTemplateMessageAsync_AssignsTemplateManualOriginToCloudRequest()
    {
        var fixture = new Fixture();

        var response = await fixture.Service.SendTemplateMessageAsync(
            fixture.Conversation.Id,
            fixture.Template.Id,
            new SendWhatsAppTemplateMessageRequest { ClientRequestId = "manual-template-send" },
            CancellationToken.None);

        Assert.True(response.Success, response.Message + " " + string.Join(",", response.Errors));
        var request = Assert.Single(fixture.CloudClient.TemplateRequests);
        Assert.Equal(WhatsAppSendOrigin.TemplateManual, request.Origin);
    }

    [Fact]
    public async Task SendLifecycleAutomationTemplateMessageAsync_AssignsReservationLifecycleOriginToCloudRequest()
    {
        var fixture = new Fixture();

        var response = await fixture.Service.SendLifecycleAutomationTemplateMessageAsync(
            fixture.CompanyId,
            fixture.Conversation.Id,
            fixture.Integration.Id,
            fixture.Template.Id,
            [],
            "reservation-lifecycle-send",
            CancellationToken.None);

        Assert.True(response.Success, response.Message + " " + string.Join(",", response.Errors));
        var request = Assert.Single(fixture.CloudClient.TemplateRequests);
        Assert.Equal(WhatsAppSendOrigin.ReservationLifecycle, request.Origin);
    }

    private sealed class Fixture
    {
        private readonly WhatsAppCloudOptions cloudOptions = new()
        {
            ProductionSendingEnabled = true,
            ManualHostProductionSendingEnabled = false,
            DevelopmentMode = false
        };

        public Fixture()
        {
            Integration = new WhatsAppIntegration
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                DisplayName = "Production WhatsApp",
                PhoneNumberId = "123456789",
                WhatsAppBusinessAccountId = "987654321",
                GraphApiVersion = "v23.0",
                IsActive = true,
                IsProductionEnabled = true
            };
            Template = new WhatsAppTemplate
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                WhatsAppIntegrationId = Integration.Id,
                Name = "arrival_details",
                LanguageCode = "en_US",
                Status = "APPROVED",
                BodyText = "Your arrival details are ready.",
                VariableCount = 0,
                IsActive = true
            };
            Conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                GuestId = Guid.NewGuid(),
                PropertyId = Guid.NewGuid(),
                Channel = GuestChannel.WhatsApp,
                ChannelIdentity = "+14155550123",
                WhatsAppIntegrationId = Integration.Id,
                Status = ConversationStatus.Open,
                HumanTakeoverEnabled = true,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                LastActivityAt = DateTimeOffset.UtcNow
            };
            ConversationRepository = new FakeConversationRepository(Conversation);
            WhatsAppRepository = new FakeWhatsAppRepository(Integration, Template);
            CloudClient = new RecordingWhatsAppCloudClient();
            Service = new WhatsAppTemplateService(
                WhatsAppRepository,
                ConversationRepository,
                new FakeCurrentTenantContext(CompanyId),
                new ConversationStatusTransitionPolicy(),
                new NoOpConversationRealtimePublisher(),
                CloudClient,
                new SuccessfulCredentialResolver(),
                new NoOpWhatsAppIntegrationHealthService(),
                new WhatsAppTemplateVariableValidator(),
                new WhatsAppOutboundSendGate(Options.Create(cloudOptions)),
                new AllowingSubscriptionEntitlementService(),
                new OpenWhatsAppCustomerServiceWindowEvaluator(),
                new PhoneNumberNormalizer(),
                new FakeHostEnvironment("Development"),
                Options.Create(cloudOptions),
                NullLogger<WhatsAppTemplateService>.Instance);
        }

        public Guid CompanyId { get; } = Guid.NewGuid();
        public WhatsAppIntegration Integration { get; }
        public WhatsAppTemplate Template { get; }
        public Conversation Conversation { get; }
        public FakeConversationRepository ConversationRepository { get; }
        public FakeWhatsAppRepository WhatsAppRepository { get; }
        public RecordingWhatsAppCloudClient CloudClient { get; }
        public WhatsAppTemplateService Service { get; }
    }

    private sealed class FakeConversationRepository(Conversation conversation) : IConversationRepository
    {
        public List<ConversationMessage> Messages { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<Conversation?> GetByIdForCompanyAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult(conversation.CompanyId == companyId && conversation.Id == conversationId ? conversation : null);

        public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, ConversationMessageProvider? provider, CancellationToken cancellationToken)
            => Task.FromResult(Messages.FirstOrDefault(message => message.CompanyId == companyId && message.ExternalMessageId == externalMessageId && (provider is null || message.Provider == provider)));

        public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

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
        public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddMessageKnowledgeSourceAsync(ConversationMessageKnowledgeSource source, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddReadStateAsync(ConversationParticipantReadState state, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeWhatsAppRepository(WhatsAppIntegration integration, WhatsAppTemplate template) : IWhatsAppRepository
    {
        public Task<WhatsAppIntegration?> GetIntegrationForCompanyAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken)
            => Task.FromResult(integration.CompanyId == companyId && integration.Id == integrationId ? integration : null);

        public Task<WhatsAppTemplate?> GetTemplateForCompanyAsync(Guid companyId, Guid integrationId, Guid templateId, CancellationToken cancellationToken)
            => Task.FromResult(template.CompanyId == companyId && template.WhatsAppIntegrationId == integrationId && template.Id == templateId ? template : null);

        public Task<WhatsAppTemplate?> GetTemplateForCompanyAsync(Guid companyId, Guid templateId, CancellationToken cancellationToken)
            => Task.FromResult(template.CompanyId == companyId && template.Id == templateId ? template : null);

        public Task<IReadOnlyCollection<WhatsAppIntegration>> ListActiveIntegrationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppIntegration?> GetActiveIntegrationByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppIntegration?> GetSoleActiveIntegrationForCompanyAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<WhatsAppIntegration>> ListIntegrationsForCompanyAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddIntegrationAsync(WhatsAppIntegration integration, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResult<WhatsAppTemplate>> ListTemplatesAsync(Guid companyId, Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppTemplate?> GetTemplateByNameAsync(Guid companyId, Guid integrationId, string name, string languageCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<WhatsAppTemplate>> ListTemplatesForIntegrationAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConversationMessage?> GetLatestInboundGuestWhatsAppMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guest>> ListActiveGuestsWithPhoneAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsForGuestAsync(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConversationMessage?> FindMessageByProviderExternalIdAsync(Guid companyId, ConversationMessageProvider provider, string externalMessageId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddTemplateAsync(WhatsAppTemplate template, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingWhatsAppCloudClient : IWhatsAppCloudClient
    {
        public List<WhatsAppTemplateSendRequest> TemplateRequests { get; } = [];

        public Task<WhatsAppSendTemplateMessageResult> SendTemplateMessageAsync(WhatsAppTemplateSendRequest request, CancellationToken cancellationToken)
        {
            TemplateRequests.Add(request);
            return Task.FromResult(new WhatsAppSendTemplateMessageResult { Success = true, ExternalMessageId = "wamid.origin-test" });
        }

        public Task<WhatsAppSendTextMessageResult> SendTextMessageAsync(WhatsAppSendTextMessageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppGetTemplatesResult> GetTemplatesAsync(WhatsAppGetTemplatesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppValidateIntegrationResult> ValidateIntegrationAsync(WhatsAppValidateIntegrationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentTenantContext(Guid companyId) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = "template-origin-test";
        public bool IsAuthenticated { get; } = true;
    }

    private sealed class SuccessfulCredentialResolver : IWhatsAppCredentialResolver
    {
        public Task<WhatsAppCredentialResolution> ResolveAsync(WhatsAppIntegration integration, CancellationToken cancellationToken)
            => Task.FromResult(new WhatsAppCredentialResolution { Success = true, AccessToken = "token" });
    }

    private sealed class NoOpWhatsAppIntegrationHealthService : IWhatsAppIntegrationHealthService
    {
        public Task<WhatsAppIntegrationHealthResponse> CheckAsync(WhatsAppIntegration integration, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AllowingSubscriptionEntitlementService : ISubscriptionEntitlementService
    {
        public Task<UsageConsumptionResult> ConsumeQuotaAsync(Guid companyId, UsageMetric metric, long quantity, string idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult(new UsageConsumptionResult(metric, null, 0, quantity, true, false));

        public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task EnsureFeatureEnabledAsync(Guid companyId, string featureKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SubscriptionSnapshot> UpdatePlanAsync(Guid companyId, Guid? planId, string? planName, string? notes, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class OpenWhatsAppCustomerServiceWindowEvaluator : IWhatsAppCustomerServiceWindowEvaluator
    {
        public Task<WhatsAppCustomerServiceWindowEvaluation> EvaluateAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult(new WhatsAppCustomerServiceWindowEvaluation { IsOpen = true });
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

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "StayFlow.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}