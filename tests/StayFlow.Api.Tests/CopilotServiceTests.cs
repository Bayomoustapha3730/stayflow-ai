using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Orchestration;

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
        Assert.NotNull(response.Data.Confidence);
        Assert.NotEmpty(response.Data.Sources);
    }

    [Fact]

    public async Task GetSuggestedRepliesAsync_ReturnsDeterministicMockReplies()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();

        fixture.Repository.Conversations.Add(conversation);

        fixture.Repository.Messages.Add(
            fixture.Repository.NewMessage(
                conversation,
                "Hi, can you share the wifi password?",
                ConversationSenderType.Guest,
                isInternal: false,
                sentAt: DateTimeOffset.UtcNow));

        var response = await fixture.Service.GetSuggestedRepliesAsync(
            conversation.Id,
            "professional",
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var suggestions = response.Data!.SuggestedReplies;

        Assert.Equal(3, suggestions.Count);

        Assert.All(
            suggestions,
            suggestion =>
            {
                Assert.False(string.IsNullOrWhiteSpace(suggestion));
                Assert.True(suggestion.Length <= 1500);
            });

        Assert.Equal(
            suggestions.Count,
            suggestions
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        Assert.Equal(1, response.Data.ContextMessageCount);

        Assert.NotNull(response.Data.Confidence);
        Assert.NotEmpty(response.Data.Sources);

        Assert.Equal("Development", response.Data.Provider);
        Assert.True(response.Data.IsMock);

        Assert.False(response.Data.FallbackUsed);
        Assert.False(response.Data.ContextTruncated);
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
    public void CopilotResponseContracts_ExposeGroundingMetadata()
    {
        Assert.NotNull(typeof(ConversationCopilotSummaryResponse).GetProperty(nameof(ConversationCopilotSummaryResponse.Confidence)));
        Assert.NotNull(typeof(ConversationCopilotSummaryResponse).GetProperty(nameof(ConversationCopilotSummaryResponse.Sources)));
        Assert.NotNull(typeof(ConversationCopilotSummaryResponse).GetProperty(nameof(ConversationCopilotSummaryResponse.Warnings)));
        Assert.NotNull(typeof(ConversationCopilotSummaryResponse).GetProperty(nameof(ConversationCopilotSummaryResponse.ContextTruncated)));

        Assert.NotNull(typeof(ConversationCopilotSuggestionsResponse).GetProperty(nameof(ConversationCopilotSuggestionsResponse.Confidence)));
        Assert.NotNull(typeof(CopilotSuggestReplyResponse).GetProperty(nameof(CopilotSuggestReplyResponse.Confidence)));
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
                new ConversationContextBuilder(
                    Repository,
                    new FakePropertyKnowledgeRepository(),
                    Options.Create(new ConversationContextLimits()),
                    NullLogger<ConversationContextBuilder>.Instance),
                new ContextConfidenceEvaluator(),
                new FakeCurrentTenantContext(CompanyId),
                new FakeReplyOrchestrator());
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

    private sealed class FakeReplyOrchestrator : IAIReplyOrchestrator
    {
        public Task<AIReplyOrchestrationResult?> OrchestrateAsync(
            Guid companyId,
            AIReplyOrchestrationRequest request,
            CancellationToken cancellationToken)
        {
            var isSuggestions = request.Operation == AIReplyOperation.SuggestedHostReplies;

            return Task.FromResult<AIReplyOrchestrationResult?>(new AIReplyOrchestrationResult
            {
                ConversationId = request.ConversationId,
                Operation = request.Operation,
                Output = isSuggestions ? null : "Thanks for your message. I will share the details shortly.",
                Suggestions = isSuggestions
                    ?
                    [
                        "Thanks for reaching out. I will share details shortly.",
                        "Could you confirm one more detail so I can provide the most accurate update?",
                        "I am reviewing this now and will provide a clear follow-up shortly."
                    ]
                    : [],
                ContextMessageCount = 1,
                Confidence = 88,
                Sources =
                [
                    new ConversationContextSource(
                        ConversationContextSourceType.Conversation,
                        null,
                        "Conversation",
                        null,
                        DateTimeOffset.UtcNow,
                        "Conversation metadata and visible message history.",
                        true)
                ],
                Warnings = [],
                Provider = "Development",
                IsMock = true,
                GeneratedAt = DateTimeOffset.UtcNow,
                ContextTruncated = false,
                FallbackUsed = false,
                CompletedStages = [AIReplyOrchestrationStage.ResultAssembled],
                DurationMilliseconds = 4,
                RequiresHumanReview = false
            });
        }
    }

    private sealed class FakePropertyKnowledgeRepository : IPropertyKnowledgeRepository
    {
        public Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
            => Task.FromResult<Property?>(null);

        public Task<PagedResult<PropertyKnowledgeArticle>> GetPagedAsync(Guid companyId, Guid propertyId, StayFlow.Api.DTOs.PropertyKnowledge.PropertyKnowledgeListQuery query, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<PropertyKnowledgeArticle?> GetByIdAsync(Guid companyId, Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<PropertyKnowledgeArticle>> GetApprovedActiveForPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<PropertyKnowledgeArticle>>([]);

        public Task AddAsync(PropertyKnowledgeArticle article, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
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

        public Task<ConversationMessage?> GetMessageForConversationAsync(Guid requestedCompanyId, Guid conversationId, Guid messageId, CancellationToken cancellationToken)
        {
            var message = Messages.FirstOrDefault(item =>
                item.CompanyId == requestedCompanyId
                && item.ConversationId == conversationId
                && item.Id == messageId);

            return Task.FromResult(message);
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

        public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, ConversationMessageProvider? provider, CancellationToken cancellationToken)
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
