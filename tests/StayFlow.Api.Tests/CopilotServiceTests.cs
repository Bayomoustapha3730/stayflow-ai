using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.Controllers;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.DTOs.Copilot;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CopilotServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ReturnsTenantScopedConversationOnly()
    {
        var fixture = new Fixture();
        var ownConversation = fixture.Repository.NewConversation();
        var crossTenantConversation = fixture.Repository.NewConversation(overrideCompanyId: Guid.NewGuid());
        fixture.Repository.Conversations.AddRange([ownConversation, crossTenantConversation]);

        var ownResponse = await fixture.Service.GetSummaryAsync(ownConversation.Id, CancellationToken.None);
        var crossTenantResponse = await fixture.Service.GetSummaryAsync(crossTenantConversation.Id, CancellationToken.None);

        Assert.True(ownResponse.Success);
        Assert.False(crossTenantResponse.Success);
        Assert.Equal("Conversation was not found.", crossTenantResponse.Message);
    }

    [Fact]
    public async Task GetSummaryAsync_ExcludesInternalNotesFromSummaryContext()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);
        fixture.Repository.Messages.AddRange(
        [
            fixture.Repository.NewMessage(conversation, "Guest asks about parking", ConversationSenderType.Guest, isInternal: false, sentAt: DateTimeOffset.UtcNow.AddMinutes(-2)),
            fixture.Repository.NewMessage(conversation, "Internal note for staff only", ConversationSenderType.System, ConversationMessageType.InternalNote, isInternal: true, sentAt: DateTimeOffset.UtcNow)
        ]);

        var response = await fixture.Service.GetSummaryAsync(conversation.Id, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(1, response.Data!.VisibleMessageCount);
        Assert.Equal("Guest asks about parking", response.Data.LatestGuestMessage);
        Assert.DoesNotContain("Internal note for staff only", response.Data.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSuggestedRepliesAsync_ReturnsDeterministicMockReplies()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);
        fixture.Repository.Messages.Add(fixture.Repository.NewMessage(
            conversation,
            "Hi, can you share the wifi password?",
            ConversationSenderType.Guest,
            isInternal: false,
            sentAt: DateTimeOffset.UtcNow));

        var response = await fixture.Service.GetSuggestedRepliesAsync(conversation.Id, "friendly", CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(3, response.Data!.SuggestedReplies.Count);
        Assert.Contains(response.Data.SuggestedReplies, item => item.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) || item.Contains("internet", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, response.Data.ContextMessageCount);
        Assert.Equal("friendly", response.Data.Tone);
    }

    [Fact]
    public async Task GetSuggestedRepliesAsync_UnsupportedToneFallsBackToProfessional()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        var response = await fixture.Service.GetSuggestedRepliesAsync(conversation.Id, "unsupported", CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("professional", response.Data!.Tone);
        Assert.Equal(3, response.Data.SuggestedReplies.Count);
        Assert.Equal(response.Data.SuggestedReplies.Count, response.Data.SuggestedReplies.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task GenerateHostReplyAsync_DoesNotPersistMessagesOrAutoSend()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);
        fixture.Repository.Messages.Add(fixture.Repository.NewMessage(
            conversation,
            "Can I check in early?",
            ConversationSenderType.Guest,
            isInternal: false,
            sentAt: DateTimeOffset.UtcNow));

        var beforeCount = fixture.Repository.Messages.Count;

        var response = await fixture.Service.GenerateHostReplyAsync(conversation.Id, new CopilotSuggestReplyRequest
        {
            Tone = "professional",
            Guidance = "Confirm available check-in windows",
            IncludeInternalNotes = false,
            MaxContextMessages = 12
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(string.IsNullOrWhiteSpace(response.Data!.SuggestedReply));
        Assert.Equal(beforeCount, fixture.Repository.Messages.Count);
    }

    [Fact]
    public async Task GenerateHostReplyAsync_BlankProviderOutputFallsBack()
    {
        var fixture = new Fixture(new BlankResponseAiProvider());
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);
        fixture.Repository.Messages.Add(fixture.Repository.NewMessage(
            conversation,
            "Can I check out late?",
            ConversationSenderType.Guest,
            isInternal: false,
            sentAt: DateTimeOffset.UtcNow));

        var response = await fixture.Service.GenerateHostReplyAsync(conversation.Id, new CopilotSuggestReplyRequest
        {
            Tone = "casual",
            Guidance = "Keep it short",
            IncludeInternalNotes = false,
            MaxContextMessages = 12
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Data!.IsFallback);
        Assert.False(string.IsNullOrWhiteSpace(response.Data.SuggestedReply));
    }

    [Fact]
    public async Task GenerateHostReplyAsync_InaccessibleConversationHandledSafely()
    {
        var fixture = new Fixture();
        var response = await fixture.Service.GenerateHostReplyAsync(Guid.NewGuid(), new CopilotSuggestReplyRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation was not found.", response.Message);
    }

    [Fact]
    public async Task GenerateHostReplyAsync_ClosedConversationReturnsValidationFailure()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        conversation.Status = ConversationStatus.Closed;
        fixture.Repository.Conversations.Add(conversation);

        var response = await fixture.Service.GenerateHostReplyAsync(conversation.Id, new CopilotSuggestReplyRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation is closed and cannot be drafted.", response.Message);
        Assert.Contains("Conversation is closed.", response.Errors);
    }

    [Fact]
    public async Task GenerateHostReplyAsync_EnforcesOutputLengthLimit()
    {
        var fixture = new Fixture(new LongResponseAiProvider());
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);
        fixture.Repository.Messages.Add(fixture.Repository.NewMessage(
            conversation,
            "Please share details",
            ConversationSenderType.Guest,
            isInternal: false,
            sentAt: DateTimeOffset.UtcNow));

        var response = await fixture.Service.GenerateHostReplyAsync(conversation.Id, new CopilotSuggestReplyRequest(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Data!.SuggestedReply.Length <= 700);
    }

    [Fact]
    public void GetSummary_RequiresConversationsReadPermission()
    {
        var method = typeof(CopilotController).GetMethod(nameof(CopilotController.GetSummary));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(RequiresPermissionAttribute), inherit: false).Cast<RequiresPermissionAttribute>());

        Assert.Equal("conversations.read", attribute.Permission);
    }

    [Fact]
    public void GetSuggestedReplies_RequiresConversationsReadPermission()
    {
        var method = typeof(CopilotController).GetMethod(nameof(CopilotController.GetSuggestedReplies));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(RequiresPermissionAttribute), inherit: false).Cast<RequiresPermissionAttribute>());

        Assert.Equal("conversations.read", attribute.Permission);
    }

    [Fact]
    public void SuggestReply_RequiresConversationsReplyPermission()
    {
        var method = typeof(CopilotController).GetMethod(nameof(CopilotController.SuggestReply));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(RequiresPermissionAttribute), inherit: false).Cast<RequiresPermissionAttribute>());

        Assert.Equal("conversations.reply", attribute.Permission);
    }

    [Fact]
    public void GenerateReply_RequiresConversationsReplyPermission()
    {
        var method = typeof(CopilotController).GetMethod(nameof(CopilotController.GenerateReply));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(RequiresPermissionAttribute), inherit: false).Cast<RequiresPermissionAttribute>());

        Assert.Equal("conversations.reply", attribute.Permission);
    }

    private sealed class Fixture
    {
        public Fixture(IAIProvider? aiProvider = null)
        {
            Repository = new FakeConversationRepository(CompanyId);
            Guest = new Guest
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                FirstName = "Demo",
                LastName = "Guest",
                PreferredLanguage = "en",
                CountryCode = "KE",
                IsActive = true
            };
            Property = new Property
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                Name = "Nairobi Loft",
                City = "Nairobi",
                CountryCode = "KE",
                AddressLine1 = "Road",
                TimeZone = "Africa/Nairobi",
                IsActive = true
            };

            Repository.Guests.Add(Guest);
            Repository.Properties.Add(Property);

            Service = new CopilotService(
                Repository,
                new FakeCurrentTenantContext(CompanyId),
                aiProvider ?? new FakeAiProvider());
        }

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guest Guest { get; }
        public Property Property { get; }
        public FakeConversationRepository Repository { get; }
        public CopilotService Service { get; }
    }

    private sealed class FakeCurrentTenantContext(Guid companyId) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = "copilot-test";
        public bool IsAuthenticated { get; } = true;
    }

    private sealed class FakeAiProvider : IAIProvider
    {
        public Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(AIProviderResult.Success(
                "Thanks for your message. I will share the details shortly.",
                "TestProvider",
                "test-model",
                "req-test",
                5));
        }
    }

    private sealed class BlankResponseAiProvider : IAIProvider
    {
        public Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(AIProviderResult.Success(
                "   ",
                "TestProvider",
                "test-model",
                "req-test",
                5));
        }
    }

    private sealed class LongResponseAiProvider : IAIProvider
    {
        public Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(AIProviderResult.Success(
                new string('A', 1200),
                "TestProvider",
                "test-model",
                "req-test",
                5));
        }
    }

    private sealed class FakeConversationRepository(Guid companyId) : IConversationRepository
    {
        public List<Conversation> Conversations { get; } = [];
        public List<ConversationMessage> Messages { get; } = [];
        public List<Guest> Guests { get; } = [];
        public List<Property> Properties { get; } = [];

        public Task<PagedResult<ConversationSummaryResponse>> ListConversationsAsync(Guid requestedCompanyId, ConversationListQueryParameters query, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<int> GetTotalUnreadCountForHostAsync(Guid companyId, Guid hostUserId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Dictionary<Guid, int>> GetUnreadMessageCountsForHostAsync(Guid companyId, Guid hostUserId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<int> GetUnreadHostMessageCountForGuestAsync(Guid companyId, Guid guestId, Guid conversationId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Conversation?> GetByIdForCompanyAsync(Guid requestedCompanyId, Guid conversationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Conversations.FirstOrDefault(conversation => conversation.CompanyId == requestedCompanyId && conversation.Id == conversationId));
        }

        public Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, DateTimeOffset cutoff, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid requestedCompanyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken)
        {
            var filtered = Messages
                .Where(message => message.CompanyId == requestedCompanyId && message.ConversationId == conversationId)
                .Where(message => query.IncludeInternal || !message.IsInternal)
                .OrderBy(message => message.SentAt)
                .ThenBy(message => message.CreatedAt)
                .ToList();

            return Task.FromResult(new PagedResult<ConversationMessage>
            {
                Items = filtered,
                PageNumber = query.NormalizedPageNumber,
                PageSize = query.NormalizedPageSize,
                TotalCount = filtered.Count
            });
        }

        public Task<ConversationMessage?> GetLatestVisibleMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<ConversationParticipantReadState?> GetReadStateAsync(Guid companyId, Guid conversationId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<ConversationParticipantReadState>> GetReadStatesForParticipantAsync(Guid companyId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Guest?> GetGuestAsync(Guid requestedCompanyId, Guid guestId, CancellationToken cancellationToken)
            => Task.FromResult(Guests.FirstOrDefault(guest => guest.CompanyId == requestedCompanyId && guest.Id == guestId));

        public Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Property?> GetPropertyAsync(Guid requestedCompanyId, Guid propertyId, CancellationToken cancellationToken)
            => Task.FromResult(Properties.FirstOrDefault(property => property.CompanyId == requestedCompanyId && property.Id == propertyId));

        public Task<User?> GetUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddReadStateAsync(ConversationParticipantReadState state, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Conversation NewConversation(Guid? overrideCompanyId = null)
        {
            var guest = Guests.Single();
            var property = Properties.Single();
            return new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = overrideCompanyId ?? companyId,
                GuestId = guest.Id,
                Guest = guest,
                PropertyId = property.Id,
                Property = property,
                Channel = GuestChannel.Web,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                LastActivityAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        public ConversationMessage NewMessage(
            Conversation conversation,
            string content,
            ConversationSenderType senderType,
            ConversationMessageType messageType = ConversationMessageType.Text,
            bool isInternal = false,
            DateTimeOffset? sentAt = null)
        {
            return new ConversationMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = conversation.CompanyId,
                ConversationId = conversation.Id,
                Conversation = conversation,
                SenderType = senderType,
                MessageType = messageType,
                Content = content,
                IsInternal = isInternal,
                SentAt = sentAt ?? DateTimeOffset.UtcNow,
                CreatedAt = sentAt ?? DateTimeOffset.UtcNow,
                UpdatedAt = sentAt ?? DateTimeOffset.UtcNow
            };
        }
    }
}
