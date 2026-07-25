using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.DTOs.Conversations;
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

public sealed class AIReplyPipelineTraceTests
{
    [Fact]
    public async Task Trace_WiFiPassword_Request_RemainsGrounded_ThroughProviderAndValidator()
    {
        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var property = new Property
        {
            Id = propertyId,
            CompanyId = companyId,
            Name = "Demo Property",
            City = "Nairobi",
            CountryCode = "KE",
            AddressLine1 = "Street 1",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        };

        var reservation = new Reservation
        {
            Id = reservationId,
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guestId,
            ConfirmationNumber = "CONF-001",
            CheckInDate = new DateOnly(2026, 8, 1),
            CheckOutDate = new DateOnly(2026, 8, 5),
            Adults = 2,
            Children = 0,
            Status = ReservationStatus.Confirmed,
            IsActive = true
        };

        var conversation = new Conversation
        {
            Id = conversationId,
            CompanyId = companyId,
            GuestId = guestId,
            PropertyId = propertyId,
            ReservationId = reservationId,
            Guest = new Guest
            {
                Id = guestId,
                CompanyId = companyId,
                FirstName = "Demo",
                LastName = "Guest",
                PreferredLanguage = "en",
                IsActive = true
            },
            Property = property,
            Reservation = reservation,
            Subject = "Wi-Fi question",
            Status = ConversationStatus.Open,
            Channel = GuestChannel.Web,
            HumanTakeoverEnabled = false
        };

        var article = new PropertyKnowledgeArticle
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            Title = "Guest Wi-Fi",
            Category = PropertyKnowledgeCategory.WiFi,
            Summary = "Primary guest network details",
            Content = "Network: StayFlowGuest\nPassword: TestWifi123!",
            Tags = "wifi,internet,network",
            Priority = 10,
            IsApproved = true,
            IsActive = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConversationId = conversationId,
            SenderType = ConversationSenderType.Guest,
            MessageType = ConversationMessageType.Text,
            Content = "What is the Wi-Fi password?",
            IsInternal = false,
            IsDeleted = false,
            SentAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var conversationRepository = new TraceConversationRepository(companyId, conversation, message);
        var knowledgeRepository = new TracePropertyKnowledgeRepository(property, article);

        var realContextBuilder = new ConversationContextBuilder(
            conversationRepository,
            knowledgeRepository,
            Options.Create(new ConversationContextLimits()),
            NullLogger<ConversationContextBuilder>.Instance);

        var traceContextBuilder = new TraceConversationContextBuilder(realContextBuilder);
        var realRanker = new PropertyKnowledgeRanker();
        var traceRanker = new TracePropertyKnowledgeRanker(realRanker);
        var realPromptBuilder = new AIPromptBuilder(Options.Create(new AIPromptOptions()));
        var tracePromptBuilder = new TracePromptBuilder(realPromptBuilder);
        var traceProvider = new TraceProvider(new DevelopmentAIProvider());
        var traceValidator = new TraceValidator(new AIReplyOutputValidator());

        var orchestrator = new AIReplyOrchestrator(
            traceContextBuilder,
            new GuestIntentDetector(),
            traceRanker,
            tracePromptBuilder,
            traceProvider,
            traceValidator,
            new AIReplySafetyEvaluator(),
            new ContextConfidenceEvaluator(),
            new AIReplyFallbackProvider(),
            Options.Create(new AIReplyOrchestratorOptions()),
            NullLogger<AIReplyOrchestrator>.Instance);

        var result = await orchestrator.OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
        {
            ConversationId = conversationId,
            Operation = AIReplyOperation.GeneratedHostReply,
            RequestedTone = "professional",
            CorrelationId = "trace-test"
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(traceContextBuilder.LastContext);
        Assert.NotNull(traceRanker.LastRanking);
        Assert.NotNull(tracePromptBuilder.LastPrompt);
        Assert.NotNull(traceProvider.LastRequest);
        Assert.NotNull(traceProvider.LastResponse);
        Assert.NotNull(traceValidator.LastResult);

        Assert.Single(traceContextBuilder.LastContext!.ApprovedKnowledgeItems);
        Assert.Equal("Guest Wi-Fi", traceContextBuilder.LastContext.ApprovedKnowledgeItems.First().Title);
        Assert.StartsWith("Network: StayFlowGuest", traceContextBuilder.LastContext.ApprovedKnowledgeItems.First().Content, StringComparison.Ordinal);

        var selected = traceRanker.LastRanking!.SelectedItems.ToList();
        Assert.Single(selected);
        Assert.Equal("Guest Wi-Fi", selected[0].Title);
        Assert.Contains(traceRanker.LastRanking.RankedItems, item => item.Item.Title == "Guest Wi-Fi" && item.Score > 0);

        Assert.Equal(GuestIntent.WiFi, result!.DetectedIntent!.Intent);

        var promptUser = tracePromptBuilder.LastPrompt!.RenderedMessages.Single(message => message.Role == "user").Content;
        Assert.Contains("Title: Guest Wi-Fi", promptUser);
        Assert.Contains("Category: WiFi", promptUser);
        Assert.Contains("Content:\nNetwork: StayFlowGuest", promptUser, StringComparison.Ordinal);

        Assert.Equal("WiFi", traceProvider.LastRequest!.DetectedIntent);
        Assert.Contains(traceProvider.LastRequest.QuestionCategories, category => category == StayFlow.Api.DTOs.AIContext.QuestionContextCategory.WiFi);
        Assert.Single(traceProvider.LastRequest.SelectedKnowledgeItems);
        Assert.Equal("Guest Wi-Fi", traceProvider.LastRequest.SelectedKnowledgeItems.First().Title);

        Assert.Equal(AIProviderOutcome.Success, traceProvider.LastResponse!.Outcome);
        Assert.Contains("StayFlowGuest", traceProvider.LastResponse.ResponseText);
        Assert.Contains("TestWifi123!", traceProvider.LastResponse.ResponseText);

        Assert.True(traceValidator.LastResult!.IsValid);

        Assert.False(result.FallbackUsed);
        Assert.False(result.RequiresHumanReview);
        Assert.Contains("StayFlowGuest", result.Output);
        Assert.Contains("TestWifi123!", result.Output);
        Assert.DoesNotContain("I can help with general stay questions", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TraceConversationContextBuilder(IConversationContextBuilder inner) : IConversationContextBuilder
    {
        public ConversationContext? LastContext { get; private set; }

        public async Task<ConversationContext?> BuildAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
        {
            LastContext = await inner.BuildAsync(companyId, conversationId, cancellationToken);
            return LastContext;
        }
    }

    private sealed class TracePropertyKnowledgeRanker(IPropertyKnowledgeRanker inner) : IPropertyKnowledgeRanker
    {
        public PropertyKnowledgeRankingResult? LastRanking { get; private set; }

        public PropertyKnowledgeRankingResult Rank(
            ConversationContext context,
            GuestIntentResult intent,
            string latestGuestMessage,
            int maxSelectedItems,
            int maxSelectedCharacters)
        {
            LastRanking = inner.Rank(context, intent, latestGuestMessage, maxSelectedItems, maxSelectedCharacters);
            return LastRanking;
        }
    }

    private sealed class TracePromptBuilder(IAIPromptBuilder inner) : IAIPromptBuilder
    {
        public AIPromptPackage? LastPrompt { get; private set; }

        public AIPromptPackage Build(AIPromptBuildRequest request)
        {
            LastPrompt = inner.Build(request);
            return LastPrompt;
        }

        public AIPromptPackage BuildReply(AIReplyPromptBuildRequest request)
        {
            LastPrompt = inner.BuildReply(request);
            return LastPrompt;
        }
    }

    private sealed class TraceProvider(IAIProvider inner) : IAIProvider
    {
        public AIProviderRequest? LastRequest { get; private set; }
        public AIProviderResult? LastResponse { get; private set; }

        public async Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastResponse = await inner.GenerateAsync(request, cancellationToken);
            return LastResponse;
        }
    }

    private sealed class TraceValidator(IAIReplyOutputValidator inner) : IAIReplyOutputValidator
    {
        public AIReplyValidationResult? LastResult { get; private set; }

        public AIReplyValidationResult Validate(
            AIReplyOperation operation,
            string? output,
            IReadOnlyCollection<string> suggestions,
            int maxOutputCharacters,
            int expectedSuggestionCount,
            bool contextIncomplete)
        {
            LastResult = inner.Validate(operation, output, suggestions, maxOutputCharacters, expectedSuggestionCount, contextIncomplete);
            return LastResult;
        }
    }

    private sealed class TraceConversationRepository(
        Guid companyId,
        Conversation conversation,
        ConversationMessage message) : IConversationRepository
    {
        public Task<PagedResult<ConversationSummaryResponse>> ListConversationsAsync(Guid companyId, ConversationListQueryParameters query, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<int> GetTotalUnreadCountForHostAsync(Guid companyId, Guid hostUserId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Dictionary<Guid, int>> GetUnreadMessageCountsForHostAsync(Guid companyId, Guid hostUserId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<int> GetUnreadHostMessageCountForGuestAsync(Guid companyId, Guid guestId, Guid conversationId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Conversation?> GetByIdForCompanyAsync(Guid requestedCompanyId, Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult(requestedCompanyId == companyId && conversationId == conversation.Id ? conversation : null);

        public Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, DateTimeOffset cutoff, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid requestedCompanyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken)
        {
            var messages = requestedCompanyId == companyId && conversationId == conversation.Id
                ? new List<ConversationMessage> { message }
                : [];

            return Task.FromResult(new PagedResult<ConversationMessage>
            {
                Items = messages,
                TotalCount = messages.Count,
                PageNumber = query.NormalizedPageNumber,
                PageSize = query.NormalizedPageSize
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

        public Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

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
    }

    private sealed class TracePropertyKnowledgeRepository(Property property, PropertyKnowledgeArticle article) : IPropertyKnowledgeRepository
    {
        public Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
            => Task.FromResult(companyId == property.CompanyId && propertyId == property.Id ? property : null);

        public Task<PagedResult<PropertyKnowledgeArticle>> GetPagedAsync(Guid companyId, Guid propertyId, StayFlow.Api.DTOs.PropertyKnowledge.PropertyKnowledgeListQuery query, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<PropertyKnowledgeArticle?> GetByIdAsync(Guid companyId, Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<PropertyKnowledgeArticle>> GetApprovedActiveForPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<PropertyKnowledgeArticle> items = companyId == property.CompanyId && propertyId == property.Id
                ? [article]
                : [];
            return Task.FromResult(items);
        }

        public Task AddAsync(PropertyKnowledgeArticle article, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
