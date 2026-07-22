using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task SendGuestMessageAsync_CreatesConversationAndPersistsAIResponse()
    {
        var fixture = new Fixture();

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("What are my dates?"), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(fixture.Repository.Conversations);
        Assert.Equal(2, fixture.Repository.Messages.Count);
        Assert.True(fixture.Orchestrator.WasCalled);
        Assert.Equal(ConversationSenderType.AI, response.Data!.AssistantMessage!.SenderType);
        Assert.Equal("DevelopmentAIProvider", response.Data.ProviderMetadata!.ProviderName);
    }

    [Fact]
    public async Task SendGuestMessageAsync_ReusesCompatibleOpenConversation()
    {
        var fixture = new Fixture();
        var existing = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(existing);

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Hello"), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(existing.Id, response.Data!.ConversationId);
        Assert.Single(fixture.Repository.Conversations);
    }

    [Fact]
    public async Task SendGuestMessageAsync_UsesValidSuppliedConversationId()
    {
        var fixture = new Fixture();
        var existing = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(existing);

        var request = fixture.Request("Hello") with { ConversationId = existing.Id };
        var response = await fixture.ChatService.SendGuestMessageAsync(request, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(existing.Id, response.Data!.ConversationId);
    }

    [Fact]
    public async Task SendGuestMessageAsync_RejectsCrossTenantConversationId()
    {
        var fixture = new Fixture();
        var existing = fixture.Repository.NewConversation(overrideCompanyId: Guid.NewGuid());
        fixture.Repository.Conversations.Add(existing);

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Hello") with { ConversationId = existing.Id }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation was not found.", response.Message);
    }

    [Fact]
    public async Task SendGuestMessageAsync_RejectsGuestMismatch()
    {
        var fixture = new Fixture();
        var existing = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(existing);

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Hello") with
        {
            ConversationId = existing.Id,
            GuestId = Guid.NewGuid()
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Guest was not found.", response.Message);
    }

    [Fact]
    public async Task SendGuestMessageAsync_RejectsChannelMismatch()
    {
        var fixture = new Fixture();
        var existing = fixture.Repository.NewConversation(channel: GuestChannel.Email, channelIdentity: "guest@example.com");
        fixture.Repository.Conversations.Add(existing);

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Hello") with { ConversationId = existing.Id }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation channel conflicts with the supplied channel.", response.Message);
    }

    [Fact]
    public async Task SendGuestMessageAsync_WhatsAppMatchingPhoneSucceeds()
    {
        var fixture = new Fixture();

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Hello") with
        {
            Channel = GuestChannel.WhatsApp,
            ChannelIdentity = "+254 700 000002"
        }, CancellationToken.None);

        Assert.True(response.Success);
    }

    [Fact]
    public async Task SendGuestMessageAsync_EmailMatchingAddressSucceedsCaseInsensitive()
    {
        var fixture = new Fixture();

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Hello") with
        {
            Channel = GuestChannel.Email,
            ChannelIdentity = " DEMO.GUEST@STAYFLOW.LOCAL "
        }, CancellationToken.None);

        Assert.True(response.Success);
    }

    [Fact]
    public async Task SendGuestMessageAsync_WebWithoutIdentitySucceeds()
    {
        var fixture = new Fixture();

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Hello"), CancellationToken.None);

        Assert.True(response.Success);
    }

    [Fact]
    public async Task SendGuestMessageAsync_ClosedConversationRejectsMessage()
    {
        var fixture = new Fixture();
        var closed = fixture.Repository.NewConversation(status: ConversationStatus.Closed);
        fixture.Repository.Conversations.Add(closed);

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Hello") with { ConversationId = closed.Id }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation state does not allow this message.", response.Message);
        Assert.False(fixture.Orchestrator.WasCalled);
    }

    [Fact]
    public async Task SendGuestMessageAsync_HumanManagedStoresGuestMessageWithoutInvokingAI()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation(status: ConversationStatus.HumanManaged, humanTakeover: true);
        fixture.Repository.Conversations.Add(conversation);

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Hello") with { ConversationId = conversation.Id }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(fixture.Orchestrator.WasCalled);
        Assert.Contains(fixture.Repository.Messages, message => message.SenderType == ConversationSenderType.Guest);
        Assert.True(response.Data!.RequiresHostAttention);
    }

    [Fact]
    public async Task SendGuestMessageAsync_DuplicateExternalMessageDoesNotInvokeAITwice()
    {
        var fixture = new Fixture();

        var first = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Hello") with { ExternalMessageId = "web-1" }, CancellationToken.None);
        fixture.Orchestrator.WasCalled = false;
        var second = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("Again") with { ExternalMessageId = "web-1" }, CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.False(fixture.Orchestrator.WasCalled);
        Assert.Single(fixture.Repository.Messages, message => message.ExternalMessageId == "web-1");
    }

    [Fact]
    public async Task GetGuestHistoryAsync_ExcludesInternalNotesAndOrdersChronologically()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);
        await fixture.ConversationService.AddGuestMessageAsync(conversation.Id, new AddGuestMessageRequest { Content = "Second", SentAt = DateTimeOffset.UtcNow.AddMinutes(2) }, CancellationToken.None);
        await fixture.ConversationService.AddInternalNoteAsync(conversation.Id, new AddInternalNoteRequest { Content = "Internal" }, CancellationToken.None);
        await fixture.ConversationService.AddHostMessageAsync(conversation.Id, new AddHostMessageRequest { Content = "First", SentAt = DateTimeOffset.UtcNow.AddMinutes(1) }, CancellationToken.None);

        var response = await fixture.ChatService.GetGuestHistoryAsync(conversation.Id, new ChatHistoryQueryParameters(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(["First", "Second"], response.Data!.Messages.Items.Select(message => message.Content));
    }

    [Fact]
    public async Task EscalateGuestConversationAsync_UpdatesState()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        var response = await fixture.ChatService.EscalateGuestConversationAsync(conversation.Id, new EscalateChatRequest { GuestId = fixture.Guest.Id, Reason = "Help" }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(ConversationStatus.Escalated, response.Data!.Status);
        Assert.True(response.Data.HumanTakeoverEnabled);
    }

    [Fact]
    public async Task EndGuestConversationAsync_ClosesConversation()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        var response = await fixture.ChatService.EndGuestConversationAsync(conversation.Id, new EndChatRequest { GuestId = fixture.Guest.Id }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(ConversationStatus.Closed, response.Data!.Status);
        Assert.NotNull(conversation.ClosedAt);
    }

    [Fact]
    public async Task SendGuestMessageAsync_NewMessageWithoutConversationDoesNotReuseClosedConversation()
    {
        var fixture = new Fixture();
        fixture.Repository.Conversations.Add(fixture.Repository.NewConversation(status: ConversationStatus.Closed));

        var response = await fixture.ChatService.SendGuestMessageAsync(fixture.Request("New"), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(2, fixture.Repository.Conversations.Count);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Repository = new FakeConversationRepository(CompanyId);
            Guest = new Guest
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                FirstName = "Demo",
                LastName = "Guest",
                Email = "demo.guest@stayflow.local",
                PhoneNumber = "+254700000002",
                PreferredLanguage = "en",
                CountryCode = "KE",
                IsActive = true
            };
            Property = new Property { Id = Guid.NewGuid(), CompanyId = CompanyId, Name = "Demo", City = "Nairobi", AddressLine1 = "Road", CountryCode = "KE", TimeZone = "Africa/Nairobi", IsActive = true };
            Repository.Guests.Add(Guest);
            Repository.Properties.Add(Property);
            TenantContext = new FakeCurrentTenantContext(CompanyId);
            ConversationService = new ConversationService(
                Repository,
                TenantContext,
                new ConversationStatusTransitionPolicy(),
                new NoOpConversationRealtimePublisher(),
                Options.Create(new ConversationOptions { MaxMessageCharacters = 2000, ReuseOpenConversationMinutes = 120, MaxHistoryMessages = 100 }));
            Orchestrator = new FakeAIOrchestrator();
            ChatService = new ChatService(
                Repository,
                ConversationService,
                Orchestrator,
                TenantContext,
                Options.Create(new ConversationOptions { MaxMessageCharacters = 2000, ReuseOpenConversationMinutes = 120, MaxHistoryMessages = 100 }));
        }

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guest Guest { get; }
        public Property Property { get; }
        public FakeConversationRepository Repository { get; }
        public FakeCurrentTenantContext TenantContext { get; }
        public ConversationService ConversationService { get; }
        public FakeAIOrchestrator Orchestrator { get; }
        public ChatService ChatService { get; }

        public SendChatMessageRequest Request(string message)
        {
            return new SendChatMessageRequest
            {
                GuestId = Guest.Id,
                PropertyId = Property.Id,
                Message = message,
                Channel = GuestChannel.Web
            };
        }
    }

    private sealed class FakeAIOrchestrator : IAIOrchestrator
    {
        public bool WasCalled { get; set; }
        public AIOrchestrationResult Result { get; set; } = new()
        {
            Outcome = AIOrchestrationOutcome.Responded,
            GuestSafeMessage = "Here is a safe answer.",
            ProviderMetadata = new AIProviderMetadata { ProviderName = "DevelopmentAIProvider", ModelName = "development", RequestId = "dev-1" }
        };

        public Task<AIOrchestrationResult> ProcessAsync(AIOrchestrationRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeCurrentTenantContext(Guid companyId) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = "chat-test";
        public bool IsAuthenticated { get; } = true;
    }

    private sealed class FakeConversationRepository(Guid companyId) : IConversationRepository
    {
        private readonly Guid companyId = companyId;

        public List<Conversation> Conversations { get; } = [];
        public List<ConversationMessage> Messages { get; } = [];
        public List<Guest> Guests { get; } = [];
        public List<Property> Properties { get; } = [];
        public List<Reservation> Reservations { get; } = [];
        public List<User> Users { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<PagedResult<ConversationSummaryResponse>> ListConversationsAsync(Guid requestedCompanyId, ConversationListQueryParameters query, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PagedResult<ConversationSummaryResponse>
            {
                Items = [],
                PageNumber = query.Page,
                PageSize = query.NormalizedPageSize,
                TotalCount = 0
            });
        }

        public Task<Conversation?> GetByIdForCompanyAsync(Guid requestedCompanyId, Guid conversationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Conversations.FirstOrDefault(conversation => conversation.CompanyId == requestedCompanyId && conversation.Id == conversationId));
        }

        public Task<Conversation?> GetOpenConversationAsync(Guid requestedCompanyId, Guid guestId, GuestChannel channel, string? channelIdentity, DateTimeOffset cutoff, CancellationToken cancellationToken)
        {
            return Task.FromResult(Conversations.FirstOrDefault(conversation =>
                conversation.CompanyId == requestedCompanyId
                && conversation.GuestId == guestId
                && conversation.Channel == channel
                && conversation.ChannelIdentity == channelIdentity
                && conversation.Status != ConversationStatus.Closed
                && conversation.LastActivityAt >= cutoff));
        }

        public Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid requestedCompanyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken)
        {
            var messages = Messages
                .Where(message => message.CompanyId == requestedCompanyId && message.ConversationId == conversationId)
                .Where(message => query.IncludeInternal || !message.IsInternal)
                .OrderBy(message => message.SentAt)
                .ToList();

            return Task.FromResult(new PagedResult<ConversationMessage>
            {
                Items = messages,
                PageNumber = 1,
                PageSize = messages.Count,
                TotalCount = messages.Count
            });
        }

        public Task<ConversationMessage?> GetLatestVisibleMessageAsync(Guid requestedCompanyId, Guid conversationId, CancellationToken cancellationToken)
        {
            var message = Messages
                .Where(item => item.CompanyId == requestedCompanyId && item.ConversationId == conversationId && !item.IsInternal && !item.IsDeleted)
                .OrderByDescending(item => item.SentAt)
                .ThenByDescending(item => item.CreatedAt)
                .FirstOrDefault();

            return Task.FromResult(message);
        }

        public Task<int> GetTotalUnreadCountForHostAsync(Guid requestedCompanyId, Guid hostUserId, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<Dictionary<Guid, int>> GetUnreadMessageCountsForHostAsync(Guid requestedCompanyId, Guid hostUserId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken)
            => Task.FromResult(new Dictionary<Guid, int>());

        public Task<int> GetUnreadHostMessageCountForGuestAsync(Guid requestedCompanyId, Guid guestId, Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<ConversationParticipantReadState?> GetReadStateAsync(Guid requestedCompanyId, Guid conversationId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken)
            => Task.FromResult<ConversationParticipantReadState?>(null);

        public Task<IReadOnlyCollection<ConversationParticipantReadState>> GetReadStatesForParticipantAsync(Guid requestedCompanyId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ConversationParticipantReadState>>([]);

        public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid requestedCompanyId, string externalMessageId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Messages.FirstOrDefault(message => message.CompanyId == requestedCompanyId && message.ExternalMessageId == externalMessageId));
        }

        public Task<Guest?> GetGuestAsync(Guid requestedCompanyId, Guid guestId, CancellationToken cancellationToken)
            => Task.FromResult(Guests.FirstOrDefault(guest => guest.CompanyId == requestedCompanyId && guest.Id == guestId && guest.IsActive));

        public Task<Reservation?> GetReservationAsync(Guid requestedCompanyId, Guid reservationId, CancellationToken cancellationToken)
            => Task.FromResult(Reservations.FirstOrDefault(reservation => reservation.CompanyId == requestedCompanyId && reservation.Id == reservationId && reservation.IsActive));

        public Task<Property?> GetPropertyAsync(Guid requestedCompanyId, Guid propertyId, CancellationToken cancellationToken)
            => Task.FromResult(Properties.FirstOrDefault(property => property.CompanyId == requestedCompanyId && property.Id == propertyId && property.IsActive));

        public Task<User?> GetUserAsync(Guid requestedCompanyId, Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(user => user.CompanyId == requestedCompanyId && user.Id == userId && user.IsActive));

        public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
        {
            Conversations.Add(conversation);
            return Task.CompletedTask;
        }

        public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task AddReadStateAsync(ConversationParticipantReadState state, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var conversation in Conversations.Where(conversation => conversation.CreatedAt == default))
            {
                conversation.CreatedAt = now;
                conversation.UpdatedAt = now;
            }

            foreach (var message in Messages.Where(message => message.CreatedAt == default))
            {
                message.CreatedAt = now;
                message.UpdatedAt = now;
            }

            return Task.CompletedTask;
        }

        public Conversation NewConversation(
            Guid? overrideCompanyId = null,
            GuestChannel channel = GuestChannel.Web,
            string? channelIdentity = null,
            ConversationStatus status = ConversationStatus.Open,
            bool humanTakeover = false)
        {
            var guest = Guests.Single();
            var property = Properties.Single();
            return new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = overrideCompanyId ?? this.companyId,
                GuestId = guest.Id,
                Guest = guest,
                PropertyId = property.Id,
                Property = property,
                Channel = channel,
                ChannelIdentity = channelIdentity,
                Status = status,
                HumanTakeoverEnabled = humanTakeover,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                LastActivityAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private sealed class NoOpConversationRealtimePublisher : IConversationRealtimePublisher
    {
        public Task PublishMessageCreatedAsync(Guid companyId, Guid conversationId, object payload, bool internalOnly, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishTypingStartedAsync(Guid companyId, Guid conversationId, object payload, bool hostOnly, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishTypingStoppedAsync(Guid companyId, Guid conversationId, object payload, bool hostOnly, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishConversationAssignedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishConversationReadStateChangedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishConversationUnreadCountChangedAsync(Guid companyId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
