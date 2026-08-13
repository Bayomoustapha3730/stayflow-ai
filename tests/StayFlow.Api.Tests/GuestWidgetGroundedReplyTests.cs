using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.Copilot;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Orchestration;
using StayFlow.Api.Services.AI.Retrieval;
using StayFlow.Api.Services.AI.Safety;
using StayFlow.Api.Services.AI.Validation;

namespace StayFlow.Api.Tests;

public sealed class GuestWidgetGroundedReplyTests
{
    [Fact]
    public async Task GuestWidget_WiFiQuestion_ReturnsApprovedNetworkAndPassword()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Guest Wi-Fi", "Network: StayFlowGuest\nPassword: DemoStay2026", approved: true, active: true));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "What is the Wi-Fi password?"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data?.AssistantMessage);
        Assert.Contains("The guest Wi-Fi network is StayFlowGuest, and the password is DemoStay2026", response.Data!.AssistantMessage!.Content);
        Assert.Equal(1, CountOccurrences(response.Data.AssistantMessage.Content, "DemoStay2026"));
    }

    [Fact]
    public async Task GuestAndHostCopilot_UseSameGroundedValues()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Guest Wi-Fi", "Network: StayFlowGuest\nPassword: DemoStay2026", approved: true, active: true));

        var chat = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "What is the Wi-Fi password?"
        }, CancellationToken.None);

        Assert.True(chat.Success);
        var conversationId = chat.Data!.ConversationId;

        var host = await fixture.CopilotService.SuggestHostReplyAsync(conversationId, new CopilotSuggestReplyRequest
        {
            Tone = "professional"
        }, CancellationToken.None);

        Assert.True(host.Success);
        Assert.Contains("StayFlowGuest", chat.Data.AssistantMessage!.Content);
        Assert.Contains("DemoStay2026", chat.Data.AssistantMessage!.Content);
        Assert.Contains("StayFlowGuest", host.Data!.SuggestedReply);
        Assert.Contains("DemoStay2026", host.Data!.SuggestedReply);
    }

    [Fact]
    public async Task GuestWidget_ExcludesUnapprovedInactiveAndCrossTenantKnowledge()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Approved Wi-Fi", "Network: StayFlowGuest\nPassword: DemoStay2026", approved: true, active: true));
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Unapproved Wi-Fi", "Password: BadPass1", approved: false, active: true));
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Inactive Wi-Fi", "Password: BadPass2", approved: true, active: false));
        fixture.Knowledge.Add(Article(Guid.NewGuid(), fixture.Property.Id, "Cross Tenant Wi-Fi", "Password: BadPass3", approved: true, active: true));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "What is the Wi-Fi password?"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var content = response.Data!.AssistantMessage!.Content;
        Assert.Contains("DemoStay2026", content);
        Assert.DoesNotContain("BadPass1", content);
        Assert.DoesNotContain("BadPass2", content);
        Assert.DoesNotContain("BadPass3", content);
    }

    [Fact]
    public async Task GuestWidget_ConflictingApprovedPasswords_DoesNotAutoSendCredential()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Wi-Fi A", "Network: StayFlowGuest\nPassword: DemoStay2026", approved: true, active: true));
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Wi-Fi B", "Network: StayFlowGuest\nPassword: DifferentPassword", approved: true, active: true));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "What is the Wi-Fi password?"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Data!.RequiresHostAttention);
        Assert.Equal(ConversationStatus.AwaitingHost, response.Data.ConversationStatus);
        Assert.Equal("I need a host or support team member to help with this request.", response.Data.AssistantMessage!.Content);
        Assert.DoesNotContain("DifferentPassword", response.Data.AssistantMessage.Content);
    }

    [Fact]
    public async Task GuestWidget_MissingExactWiFiDetails_UsesSafeFallback()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Wi-Fi", "Wi-Fi is available for guests.", approved: true, active: true));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "What is the Wi-Fi password?"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Contains("Host verification is required", response.Data!.AssistantMessage!.Content);
    }

    [Fact]
    public async Task GuestWidget_CheckInInformation_DoesNotSelectEmergencyGuidance()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(
            fixture.CompanyId,
            fixture.Property.Id,
            "Emergency guidance",
            "If there is a fire, leave immediately and call emergency services.",
            approved: true,
            active: true,
            category: PropertyKnowledgeCategory.Emergency,
            priority: 10));
        fixture.Knowledge.Add(Article(
            fixture.CompanyId,
            fixture.Property.Id,
            "Check-in details",
            "Check-in starts at 3:00 PM. Use the keypad code in your arrival message.",
            approved: true,
            active: true,
            category: PropertyKnowledgeCategory.CheckIn,
            priority: 9));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "Check-in Information"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var content = response.Data!.AssistantMessage!.Content;
        Assert.Contains("Check-in starts at 3:00 PM", content);
        Assert.DoesNotContain("fire", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuestWidget_EmergencyQuery_SelectsEmergencyGuidanceFirst()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(
            fixture.CompanyId,
            fixture.Property.Id,
            "Check-in details",
            "Check-in starts at 3:00 PM.",
            approved: true,
            active: true,
            category: PropertyKnowledgeCategory.CheckIn,
            priority: 10));
        fixture.Knowledge.Add(Article(
            fixture.CompanyId,
            fixture.Property.Id,
            "Emergency guidance",
            "If there is immediate danger, contact local emergency services now.",
            approved: true,
            active: true,
            category: PropertyKnowledgeCategory.Emergency,
            priority: 6));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "There is a fire"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var content = response.Data!.AssistantMessage!.Content;
        Assert.Contains("emergency services", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuestWidget_AmbiguousAccessQuestion_AsksClarification()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Check-in details", "Use the smart lock for entry.", approved: true, active: true, category: PropertyKnowledgeCategory.CheckIn));
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Guest Wi-Fi", "Network: StayFlowGuest\nPassword: DemoStay2026", approved: true, active: true, category: PropertyKnowledgeCategory.WiFi));
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Parking", "Garage B is assigned to your unit.", approved: true, active: true, category: PropertyKnowledgeCategory.Parking));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "Tell me about access"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var content = response.Data!.AssistantMessage!.Content;
        Assert.Equal("Are you asking about check-in time, property entry, or Wi-Fi access?", content);
        AssertNoInternalIdentifiers(content);
        Assert.DoesNotContain("fire", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("access")]
    [InlineData("property access")]
    [InlineData("access information")]
    [InlineData("access instructions")]
    [InlineData("access details")]
    [InlineData("building access")]
    [InlineData("entry information")]
    [InlineData("entering")]
    [InlineData("getting in")]
    [InlineData("get in")]
    [InlineData("getting inside")]
    [InlineData("I need access information")]
    [InlineData("How does access work?")]
    [InlineData("I need access details")]
    [InlineData("Can you share access instructions?")]
    [InlineData("I have a question about property access")]
    [InlineData("Can you explain building access?")]
    [InlineData("Access work?")]
    public async Task GuestWidget_AmbiguousAccessVariants_AskClarification(string message)
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Check-in details", "Use the smart lock for entry.", approved: true, active: true, category: PropertyKnowledgeCategory.CheckIn));
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Guest Wi-Fi", "Network: StayFlowGuest\nPassword: DemoStay2026", approved: true, active: true, category: PropertyKnowledgeCategory.WiFi));
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Parking", "Garage B is assigned to your unit.", approved: true, active: true, category: PropertyKnowledgeCategory.Parking));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = message
        }, CancellationToken.None);

        Assert.True(response.Success);
        var content = response.Data!.AssistantMessage!.Content;
        Assert.Equal("Are you asking about check-in time, property entry, or Wi-Fi access?", content);
        AssertNoInternalIdentifiers(content);
    }

    [Fact]
    public async Task GuestWidget_PetPolicyMissing_ReturnsKnowledgeUnavailableWithoutClarificationEnums()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(
            fixture.CompanyId,
            fixture.Property.Id,
            "House rules",
            "Quiet hours are 10 PM to 8 AM. No smoking.",
            approved: true,
            active: true,
            category: PropertyKnowledgeCategory.HouseRules));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "Can I bring pets?"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var content = response.Data!.AssistantMessage!.Content;
        Assert.Equal("I couldn't find a pet policy for this property. I can notify the host to confirm whether pets are allowed.", content);
        AssertNoInternalIdentifiers(content);
    }

    [Fact]
    public async Task GuestWidget_HighConfidencePetPolicyMissing_DoesNotAskForClarification()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(
            fixture.CompanyId,
            fixture.Property.Id,
            "House rules",
            "Quiet hours are 10 PM to 8 AM. No smoking.",
            approved: true,
            active: true,
            category: PropertyKnowledgeCategory.HouseRules));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "Do you allow dogs?"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var content = response.Data!.AssistantMessage!.Content;
        Assert.Equal("I couldn't find a pet policy for this property. I can notify the host to confirm whether pets are allowed.", content);
        Assert.DoesNotContain("Are you asking", content, StringComparison.OrdinalIgnoreCase);
        AssertNoInternalIdentifiers(content);
    }

    [Fact]
    public async Task GuestWidget_UnknownCurtainQuestion_ReturnsNoKnowledgeWithoutFabrication()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(
            fixture.CompanyId,
            fixture.Property.Id,
            "Check-in details",
            "Check-in starts at 3:00 PM.",
            approved: true,
            active: true,
            category: PropertyKnowledgeCategory.CheckIn));

        var response = await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "What color are the curtains?"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var content = response.Data!.AssistantMessage!.Content;
        Assert.Equal("I don't have information about the curtain color. I can ask the host if you'd like.", content);
        AssertNoInternalIdentifiers(content);
        Assert.DoesNotContain("3:00 PM", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuestWidget_GuestVisibleResponses_NeverExposeInternalIntentIdentifiers()
    {
        var fixture = new Fixture();
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Check-in details", "Use the smart lock for entry.", approved: true, active: true, category: PropertyKnowledgeCategory.CheckIn));
        fixture.Knowledge.Add(Article(fixture.CompanyId, fixture.Property.Id, "Guest Wi-Fi", "Network: StayFlowGuest\nPassword: DemoStay2026", approved: true, active: true, category: PropertyKnowledgeCategory.WiFi));

        var responses = new List<string>();
        responses.Add((await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "Tell me about access"
        }, CancellationToken.None)).Data!.AssistantMessage!.Content);

        responses.Add((await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "Can I bring pets?"
        }, CancellationToken.None)).Data!.AssistantMessage!.Content);

        responses.Add((await fixture.ChatService.SendGuestMessageAsync(new SendChatMessageRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web,
            Message = "What color are the curtains?"
        }, CancellationToken.None)).Data!.AssistantMessage!.Content);

        Assert.All(responses, AssertNoInternalIdentifiers);
    }

    private static PropertyKnowledgeArticle Article(
        Guid companyId,
        Guid propertyId,
        string title,
        string content,
        bool approved,
        bool active,
        PropertyKnowledgeCategory category = PropertyKnowledgeCategory.WiFi,
        int priority = 10)
    {
        return new PropertyKnowledgeArticle
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            Category = category,
            Title = title,
            Summary = null,
            Content = content,
            Tags = category switch
            {
                PropertyKnowledgeCategory.WiFi => "wifi,network,password",
                PropertyKnowledgeCategory.CheckIn => "check-in,arrival,entry",
                PropertyKnowledgeCategory.Checkout => "checkout,departure",
                PropertyKnowledgeCategory.Parking => "parking,garage,car",
                PropertyKnowledgeCategory.HouseRules => "rules,quiet hours",
                PropertyKnowledgeCategory.LocalRecommendations => "restaurant,nearby,recommendations",
                PropertyKnowledgeCategory.Emergency => "emergency,fire,safety",
                _ => "property,info"
            },
            Priority = priority,
            IsApproved = approved,
            IsActive = active,
            IsDeleted = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static int CountOccurrences(string text, string token)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while (true)
        {
            index = text.IndexOf(token, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            index += token.Length;
        }
    }

    private static void AssertNoInternalIdentifiers(string content)
    {
        Assert.DoesNotContain("PetPolicy", content, StringComparison.Ordinal);
        Assert.DoesNotContain("HouseRules", content, StringComparison.Ordinal);
        Assert.DoesNotContain("PropertyAccess", content, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalRecommendations", content, StringComparison.Ordinal);
        Assert.DoesNotContain("GuestIntent", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ReasonCode", content, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Repository = new InMemoryConversationRepository();
            KnowledgeRepository = new InMemoryPropertyKnowledgeRepository();
            Knowledge = KnowledgeRepository.Items;

            Guest = new Guest
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                FirstName = "Demo",
                LastName = "Guest",
                Email = "guest@example.com",
                PreferredLanguage = "en",
                IsActive = true
            };

            Property = new Property
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                Name = "Demo Property",
                City = "Nairobi",
                CountryCode = "KE",
                AddressLine1 = "Street 1",
                TimeZone = "Africa/Nairobi",
                IsActive = true
            };

            Repository.Guests.Add(Guest);
            Repository.Properties.Add(Property);

            var tenant = new FakeCurrentTenantContext(CompanyId);
            ConversationService = new ConversationService(
                Repository,
                tenant,
                new ConversationStatusTransitionPolicy(),
                new NoOpConversationRealtimePublisher(),
                new NoOpConversationChannelDispatcher(),
                Options.Create(new ConversationOptions { MaxMessageCharacters = 2000, ReuseOpenConversationMinutes = 120, MaxHistoryMessages = 100 }));

            var replyOrchestrator = new AIReplyOrchestrator(
                new ConversationContextBuilder(
                    Repository,
                    KnowledgeRepository,
                    Options.Create(new ConversationContextLimits()),
                    NullLogger<ConversationContextBuilder>.Instance),
                new PropertyKnowledgeRanker(
                    Options.Create(new KnowledgeRetrievalOptions()),
                    new DeterministicKnowledgeSimilarityScorer()),
                new AIPromptBuilder(Options.Create(new AIPromptOptions())),
                new DevelopmentAIProvider(),
                new AIReplyOutputValidator(),
                new AIReplySafetyEvaluator(),
                new ContextConfidenceEvaluator(),
                new AIReplyFallbackProvider(),
                Options.Create(new AIReplyOrchestratorOptions()),
                NullLogger<AIReplyOrchestrator>.Instance);

            ChatService = new ChatService(
                Repository,
                ConversationService,
                replyOrchestrator,
                tenant,
                Options.Create(new ConversationOptions { MaxMessageCharacters = 2000, ReuseOpenConversationMinutes = 120, MaxHistoryMessages = 100 }));

            CopilotService = new CopilotService(
                new ConversationContextBuilder(
                    Repository,
                    KnowledgeRepository,
                    Options.Create(new ConversationContextLimits()),
                    NullLogger<ConversationContextBuilder>.Instance),
                new ContextConfidenceEvaluator(),
                tenant,
                replyOrchestrator);
        }

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guest Guest { get; }
        public Property Property { get; }
        public InMemoryConversationRepository Repository { get; }
        public InMemoryPropertyKnowledgeRepository KnowledgeRepository { get; }
        public List<PropertyKnowledgeArticle> Knowledge { get; }
        public ConversationService ConversationService { get; }
        public ChatService ChatService { get; }
        public CopilotService CopilotService { get; }
    }

    private sealed class FakeCurrentTenantContext(Guid companyId) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = "guest-grounding-test";
        public bool IsAuthenticated { get; } = true;
    }

    private sealed class InMemoryPropertyKnowledgeRepository : IPropertyKnowledgeRepository
    {
        public List<PropertyKnowledgeArticle> Items { get; } = [];

        public Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
            => Task.FromResult<Property?>(null);

        public Task<PagedResult<PropertyKnowledgeArticle>> GetPagedAsync(Guid companyId, Guid propertyId, StayFlow.Api.DTOs.PropertyKnowledge.PropertyKnowledgeListQuery query, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<PropertyKnowledgeArticle?> GetByIdAsync(Guid companyId, Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<PropertyKnowledgeArticle>> GetApprovedActiveForPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<PropertyKnowledgeArticle> result = Items
                .Where(item => item.CompanyId == companyId
                    && item.PropertyId == propertyId
                    && item.IsApproved
                    && item.IsActive
                    && !item.IsDeleted)
                .OrderByDescending(item => item.Priority)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.Title)
                .ToList();

            return Task.FromResult(result);
        }

        public Task AddAsync(PropertyKnowledgeArticle article, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class InMemoryConversationRepository : IConversationRepository
    {
        public List<Conversation> Conversations { get; } = [];
        public List<ConversationMessage> Messages { get; } = [];
        public List<Guest> Guests { get; } = [];
        public List<Property> Properties { get; } = [];

        public Task<PagedResult<ConversationSummaryResponse>> ListConversationsAsync(Guid companyId, ConversationListQueryParameters query, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<int> GetTotalUnreadCountForHostAsync(Guid companyId, Guid hostUserId, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<Dictionary<Guid, int>> GetUnreadMessageCountsForHostAsync(Guid companyId, Guid hostUserId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken)
            => Task.FromResult(new Dictionary<Guid, int>());

        public Task<int> GetUnreadHostMessageCountForGuestAsync(Guid companyId, Guid guestId, Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<Conversation?> GetByIdForCompanyAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
        {
            var conversation = Conversations.FirstOrDefault(item => item.CompanyId == companyId && item.Id == conversationId);
            if (conversation is null)
            {
                return Task.FromResult<Conversation?>(null);
            }

            conversation.Guest = Guests.First(item => item.Id == conversation.GuestId);
            conversation.Property = conversation.PropertyId.HasValue ? Properties.First(item => item.Id == conversation.PropertyId.Value) : null;
            return Task.FromResult<Conversation?>(conversation);
        }

        public Task<ConversationMessage?> GetMessageForConversationAsync(Guid companyId, Guid conversationId, Guid messageId, CancellationToken cancellationToken)
        {
            var message = Messages.FirstOrDefault(item =>
                item.CompanyId == companyId
                && item.ConversationId == conversationId
                && item.Id == messageId);

            return Task.FromResult(message);
        }

        public Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, DateTimeOffset cutoff, CancellationToken cancellationToken)
        {
            var conversation = Conversations.FirstOrDefault(item => item.CompanyId == companyId
                && item.GuestId == guestId
                && item.Channel == channel
                && item.ChannelIdentity == channelIdentity
                && item.Status != ConversationStatus.Closed
                && item.LastActivityAt >= cutoff);
            return Task.FromResult(conversation);
        }

        public Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid companyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken)
        {
            var filtered = Messages
                .Where(item => item.CompanyId == companyId && item.ConversationId == conversationId)
                .Where(item => query.IncludeInternal || !item.IsInternal)
                .OrderBy(item => item.SentAt)
                .ThenBy(item => item.CreatedAt)
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
            => Task.FromResult(Messages.Where(item => item.CompanyId == companyId && item.ConversationId == conversationId && !item.IsInternal && !item.IsDeleted)
                .OrderByDescending(item => item.SentAt)
                .ThenByDescending(item => item.CreatedAt)
                .FirstOrDefault());

        public Task<ConversationParticipantReadState?> GetReadStateAsync(Guid companyId, Guid conversationId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken)
            => Task.FromResult<ConversationParticipantReadState?>(null);

        public Task<IReadOnlyCollection<ConversationParticipantReadState>> GetReadStatesForParticipantAsync(Guid companyId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ConversationParticipantReadState>>([]);

        public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, ConversationMessageProvider? provider, CancellationToken cancellationToken)
            => Task.FromResult(Messages.FirstOrDefault(item => item.CompanyId == companyId && item.ExternalMessageId == externalMessageId && (provider is null || item.Provider == provider)));

        public Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken)
            => Task.FromResult(Guests.FirstOrDefault(item => item.CompanyId == companyId && item.Id == guestId && item.IsActive));

        public Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken)
            => Task.FromResult<Reservation?>(null);

        public Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
            => Task.FromResult(Properties.FirstOrDefault(item => item.CompanyId == companyId && item.Id == propertyId && item.IsActive));

        public Task<User?> GetUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<User?>(null);

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
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Conversation NewConversation(Guid companyId, Guid guestId, Guid propertyId, GuestChannel channel = GuestChannel.Web)
        {
            return new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                GuestId = guestId,
                PropertyId = propertyId,
                Channel = channel,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            };
        }

        public ConversationMessage NewMessage(Conversation conversation, string content, ConversationSenderType senderType)
        {
            return new ConversationMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = conversation.CompanyId,
                ConversationId = conversation.Id,
                SenderType = senderType,
                MessageType = ConversationMessageType.Text,
                Content = content,
                SentAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                IsInternal = false,
                IsDeleted = false
            };
        }
    }

    private sealed class NoOpConversationRealtimePublisher : IConversationRealtimePublisher
    {
        public Task PublishMessageCreatedAsync(Guid companyId, Guid conversationId, object payload, bool internalOnly, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishMessageUpdatedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishTypingStartedAsync(Guid companyId, Guid conversationId, object payload, bool hostOnly, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishTypingStoppedAsync(Guid companyId, Guid conversationId, object payload, bool hostOnly, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishConversationStateChangedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishConversationAssignedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishConversationReadStateChangedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishConversationUnreadCountChangedAsync(Guid companyId, object payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishMessageDeliveryUpdatedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PublishHostCopilotWorkspaceUpdatedAsync(Guid companyId, object payload, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class NoOpConversationChannelDispatcher : IConversationChannelDispatcher
    {
        public Task DispatchOutboundMessageAsync(Conversation conversation, ConversationMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
