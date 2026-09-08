using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

/// <summary>
/// Exercises the sender against the real customer-service-window evaluator so the persisted
/// inbound provider actually decides whether free-form outbound is allowed.
/// </summary>
public sealed class WhatsAppConversationChannelSenderWindowTests
{
    [Fact]
    public async Task SendAsync_FreshQualifyingInbound_FreeFormReachesLocalSendingStage()
    {
        var fixture = new Fixture();
        fixture.AddInbound(DateTimeOffset.UtcNow.AddMinutes(-2), ConversationMessageProvider.WhatsAppCloud);
        var message = Fixture.NewOutbound();

        await fixture.Sender.SendAsync(fixture.Conversation, message, WhatsAppSendOrigin.AiConcierge, CancellationToken.None);

        Assert.Equal(ConversationMessageDeliveryStatus.Sent, message.DeliveryStatus);
        Assert.Null(message.FailureCode);
        Assert.Equal(1, fixture.CloudClient.TextSendCount);
    }

    [Fact]
    public async Task SendAsync_NoQualifyingInbound_FreeFormRejectedAsCustomerServiceWindowClosed()
    {
        var fixture = new Fixture();
        var message = Fixture.NewOutbound();

        await fixture.Sender.SendAsync(fixture.Conversation, message, WhatsAppSendOrigin.AiConcierge, CancellationToken.None);

        Assert.Equal(ConversationMessageDeliveryStatus.Failed, message.DeliveryStatus);
        Assert.Equal("CustomerServiceWindowClosed", message.FailureCode);
        Assert.Equal(0, fixture.CloudClient.TextSendCount);
    }

    [Fact]
    public async Task SendAsync_InboundStoredWithProviderNone_FreeFormRejectedAsCustomerServiceWindowClosed()
    {
        var fixture = new Fixture();
        fixture.AddInbound(DateTimeOffset.UtcNow.AddMinutes(-2), ConversationMessageProvider.None);
        var message = Fixture.NewOutbound();

        await fixture.Sender.SendAsync(fixture.Conversation, message, WhatsAppSendOrigin.AiConcierge, CancellationToken.None);

        Assert.Equal("CustomerServiceWindowClosed", message.FailureCode);
        Assert.Equal(0, fixture.CloudClient.TextSendCount);
    }

    [Fact]
    public async Task SendAsync_StaleInbound_FreeFormRejectedAsCustomerServiceWindowClosed()
    {
        var fixture = new Fixture();
        fixture.AddInbound(DateTimeOffset.UtcNow.AddHours(-25), ConversationMessageProvider.WhatsAppCloud);
        var message = Fixture.NewOutbound();

        await fixture.Sender.SendAsync(fixture.Conversation, message, WhatsAppSendOrigin.AiConcierge, CancellationToken.None);

        Assert.Equal("CustomerServiceWindowClosed", message.FailureCode);
        Assert.Equal(0, fixture.CloudClient.TextSendCount);
    }

    [Fact]
    public async Task SendAsync_TemplateMessageOutsideWindow_IsNotBlockedByWindowGate()
    {
        var fixture = new Fixture();
        var message = Fixture.NewOutbound();
        message.IsTemplateMessage = true;

        await fixture.Sender.SendAsync(fixture.Conversation, message, WhatsAppSendOrigin.GuestJourney, CancellationToken.None);

        Assert.NotEqual("CustomerServiceWindowClosed", message.FailureCode);
        Assert.Equal(ConversationMessageDeliveryStatus.Sent, message.DeliveryStatus);
    }

    private sealed class Fixture
    {
        private readonly List<ConversationMessage> messages = [];

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
                Id = ConversationId,
                CompanyId = CompanyId,
                GuestId = Guid.NewGuid(),
                Channel = GuestChannel.WhatsApp,
                ChannelIdentity = "+14155551234",
                WhatsAppIntegrationId = Integration.Id,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                LastActivityAt = DateTimeOffset.UtcNow
            };

            var repository = new FakeWhatsAppRepository(messages, Integration);
            // DevelopmentMode keeps the gate local; the fake client guarantees no provider traffic.
            var options = Options.Create(new WhatsAppCloudOptions
            {
                DevelopmentMode = true,
                CustomerServiceWindowHours = 24
            });

            Sender = new WhatsAppConversationChannelSender(
                CloudClient,
                repository,
                new SuccessfulCredentialResolver(),
                new WhatsAppCustomerServiceWindowEvaluator(repository, options),
                new WhatsAppOutboundSendGate(options),
                new PhoneNumberNormalizer(),
                NullLogger<WhatsAppConversationChannelSender>.Instance);
        }

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid ConversationId { get; } = Guid.NewGuid();
        public WhatsAppIntegration Integration { get; }
        public Conversation Conversation { get; }
        public RecordingWhatsAppCloudClient CloudClient { get; } = new();
        public WhatsAppConversationChannelSender Sender { get; }

        public void AddInbound(DateTimeOffset sentAt, ConversationMessageProvider provider)
        {
            messages.Add(new ConversationMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                ConversationId = ConversationId,
                SenderType = ConversationSenderType.Guest,
                MessageType = ConversationMessageType.Text,
                Content = "Inbound guest message",
                Provider = provider,
                SentAt = sentAt.ToUniversalTime()
            });
        }

        public static ConversationMessage NewOutbound()
        {
            return new ConversationMessage
            {
                Id = Guid.NewGuid(),
                SenderType = ConversationSenderType.AI,
                MessageType = ConversationMessageType.Text,
                Content = "Automated reply",
                SentAt = DateTimeOffset.UtcNow
            };
        }
    }

    private sealed class RecordingWhatsAppCloudClient : IWhatsAppCloudClient
    {
        public int TextSendCount { get; private set; }

        public Task<WhatsAppSendTextMessageResult> SendTextMessageAsync(WhatsAppSendTextMessageRequest request, CancellationToken cancellationToken)
        {
            TextSendCount++;
            return Task.FromResult(new WhatsAppSendTextMessageResult
            {
                Success = true,
                ExternalMessageId = "dev-wa-local-test"
            });
        }

        public Task<WhatsAppGetTemplatesResult> GetTemplatesAsync(WhatsAppGetTemplatesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppSendTemplateMessageResult> SendTemplateMessageAsync(WhatsAppTemplateSendRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppValidateIntegrationResult> ValidateIntegrationAsync(WhatsAppValidateIntegrationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class SuccessfulCredentialResolver : IWhatsAppCredentialResolver
    {
        public Task<WhatsAppCredentialResolution> ResolveAsync(WhatsAppIntegration integration, CancellationToken cancellationToken)
            => Task.FromResult(new WhatsAppCredentialResolution { Success = true, AccessToken = "local-test-token" });
    }

    // Mirrors the production EF predicate in WhatsAppRepository.GetLatestInboundGuestWhatsAppMessageAsync.
    private sealed class FakeWhatsAppRepository(IReadOnlyCollection<ConversationMessage> messages, WhatsAppIntegration integration) : IWhatsAppRepository
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
}
