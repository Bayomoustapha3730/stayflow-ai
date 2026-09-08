using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppCustomerServiceWindowEvaluatorTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ConversationId = Guid.NewGuid();

    [Fact]
    public async Task EvaluateAsync_FreshQualifyingInbound_WindowIsOpen()
    {
        var now = DateTimeOffset.UtcNow;
        var fixture = new Fixture(NewInbound(now.AddMinutes(-5)));

        var evaluation = await fixture.Evaluator.EvaluateAsync(CompanyId, ConversationId, CancellationToken.None);

        Assert.True(evaluation.IsOpen);
        Assert.Equal(now.AddMinutes(-5).ToUniversalTime(), evaluation.LastInboundAt);
        Assert.Equal(now.AddMinutes(-5).ToUniversalTime().AddHours(24), evaluation.ExpiresAt);
    }

    [Fact]
    public async Task EvaluateAsync_StaleQualifyingInbound_WindowIsClosed()
    {
        var fixture = new Fixture(NewInbound(DateTimeOffset.UtcNow.AddHours(-24).AddMinutes(-1)));

        var evaluation = await fixture.Evaluator.EvaluateAsync(CompanyId, ConversationId, CancellationToken.None);

        Assert.False(evaluation.IsOpen);
        Assert.Equal("Customer-service window has expired.", evaluation.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_NoInboundMessage_WindowIsClosed()
    {
        var fixture = new Fixture();

        var evaluation = await fixture.Evaluator.EvaluateAsync(CompanyId, ConversationId, CancellationToken.None);

        Assert.False(evaluation.IsOpen);
        Assert.Null(evaluation.LastInboundAt);
        Assert.Equal("No inbound WhatsApp guest message is available to open the customer-service window.", evaluation.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_NewInboundAfterExpiredInbound_WindowReopens()
    {
        var expired = NewInbound(DateTimeOffset.UtcNow.AddDays(-3));
        var fresh = NewInbound(DateTimeOffset.UtcNow.AddMinutes(-1));
        var fixture = new Fixture(expired, fresh);

        var evaluation = await fixture.Evaluator.EvaluateAsync(CompanyId, ConversationId, CancellationToken.None);

        Assert.True(evaluation.IsOpen);
        Assert.Equal(fresh.SentAt, evaluation.LastInboundAt);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleInboundMessages_LatestDeterminesExpiry()
    {
        var older = NewInbound(DateTimeOffset.UtcNow.AddHours(-10));
        var latest = NewInbound(DateTimeOffset.UtcNow.AddHours(-2));
        var middle = NewInbound(DateTimeOffset.UtcNow.AddHours(-6));
        var fixture = new Fixture(older, latest, middle);

        var evaluation = await fixture.Evaluator.EvaluateAsync(CompanyId, ConversationId, CancellationToken.None);

        Assert.True(evaluation.IsOpen);
        Assert.Equal(latest.SentAt, evaluation.LastInboundAt);
        Assert.Equal(latest.SentAt.AddHours(24), evaluation.ExpiresAt);
    }

    [Fact]
    public async Task EvaluateAsync_ProviderNoneInbound_DoesNotQualify()
    {
        var fixture = new Fixture(NewInbound(DateTimeOffset.UtcNow.AddMinutes(-5), provider: ConversationMessageProvider.None));

        var evaluation = await fixture.Evaluator.EvaluateAsync(CompanyId, ConversationId, CancellationToken.None);

        Assert.False(evaluation.IsOpen);
        Assert.Null(evaluation.LastInboundAt);
    }

    [Fact]
    public async Task EvaluateAsync_NonGuestSender_DoesNotQualify()
    {
        var fixture = new Fixture(NewInbound(DateTimeOffset.UtcNow.AddMinutes(-5), senderType: ConversationSenderType.AI));

        var evaluation = await fixture.Evaluator.EvaluateAsync(CompanyId, ConversationId, CancellationToken.None);

        Assert.False(evaluation.IsOpen);
    }

    [Fact]
    public async Task EvaluateAsync_InboundFromAnotherCompany_DoesNotOpenEvaluatedTenantWindow()
    {
        var fixture = new Fixture(NewInbound(DateTimeOffset.UtcNow.AddMinutes(-5), companyId: Guid.NewGuid()));

        var evaluation = await fixture.Evaluator.EvaluateAsync(CompanyId, ConversationId, CancellationToken.None);

        Assert.False(evaluation.IsOpen);
        Assert.Null(evaluation.LastInboundAt);
    }

    [Fact]
    public async Task EvaluateAsync_InboundFromAnotherConversation_DoesNotOpenEvaluatedConversationWindow()
    {
        var fixture = new Fixture(NewInbound(DateTimeOffset.UtcNow.AddMinutes(-5), conversationId: Guid.NewGuid()));

        var evaluation = await fixture.Evaluator.EvaluateAsync(CompanyId, ConversationId, CancellationToken.None);

        Assert.False(evaluation.IsOpen);
        Assert.Null(evaluation.LastInboundAt);
    }

    [Fact]
    public async Task EvaluateAsync_DeletedInbound_DoesNotQualify()
    {
        var fixture = new Fixture(NewInbound(DateTimeOffset.UtcNow.AddMinutes(-5), isDeleted: true));

        var evaluation = await fixture.Evaluator.EvaluateAsync(CompanyId, ConversationId, CancellationToken.None);

        Assert.False(evaluation.IsOpen);
    }

    private static ConversationMessage NewInbound(
        DateTimeOffset sentAt,
        ConversationMessageProvider provider = ConversationMessageProvider.WhatsAppCloud,
        ConversationSenderType senderType = ConversationSenderType.Guest,
        Guid? companyId = null,
        Guid? conversationId = null,
        bool isDeleted = false)
    {
        return new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? CompanyId,
            ConversationId = conversationId ?? ConversationId,
            SenderType = senderType,
            MessageType = ConversationMessageType.Text,
            Content = "Inbound guest message",
            Provider = provider,
            IsDeleted = isDeleted,
            SentAt = sentAt.ToUniversalTime()
        };
    }

    private sealed class Fixture
    {
        public Fixture(params ConversationMessage[] messages)
        {
            Evaluator = new WhatsAppCustomerServiceWindowEvaluator(
                new FakeWhatsAppRepository(messages),
                Options.Create(new WhatsAppCloudOptions { CustomerServiceWindowHours = 24 }));
        }

        public WhatsAppCustomerServiceWindowEvaluator Evaluator { get; }
    }

    // Mirrors the production EF predicate in WhatsAppRepository.GetLatestInboundGuestWhatsAppMessageAsync.
    private sealed class FakeWhatsAppRepository(IReadOnlyCollection<ConversationMessage> messages) : IWhatsAppRepository
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

        public Task<IReadOnlyCollection<WhatsAppIntegration>> ListActiveIntegrationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppIntegration?> GetActiveIntegrationByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppIntegration?> GetSoleActiveIntegrationForCompanyAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WhatsAppIntegration?> GetIntegrationForCompanyAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<WhatsAppIntegration>> ListIntegrationsForCompanyAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddIntegrationAsync(WhatsAppIntegration integration, CancellationToken cancellationToken) => throw new NotSupportedException();
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
