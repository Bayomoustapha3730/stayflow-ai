using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Orchestration;
using StayFlow.Api.Services.AI.Retrieval;
using StayFlow.Api.Services.AI.Safety;
using StayFlow.Api.Services.AI.Validation;

namespace StayFlow.Api.Tests;

public sealed class AIReplyOrchestratorGroundingTests
{
    [Fact]
    public async Task OrchestrateAsync_WiFiReply_UsesSelectedApprovedKnowledgeAndSource()
    {
        var selected = new ConversationContextKnowledgeItem(
            "wifi-source-1",
            "Guest Wi-Fi",
            "Network: StayFlowGuest\nPassword: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            10,
            true,
            ["wifi", "network"],
            "Guest wireless details");

        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider();
        var orchestrator = BuildOrchestrator(context, provider, selectedItems: [selected]);

        var result = await orchestrator.OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.GeneratedHostReply,
            RequestedTone = "professional"
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.FallbackUsed);
        Assert.False(result.RequiresHumanReview);
        Assert.Contains("StayFlowGuest", result.Output);
        Assert.Contains("DemoStay2026", result.Output);
        Assert.DoesNotContain("information is available", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Sources, source => source.SourceType == ConversationContextSourceType.PropertyKnowledge && source.SourceId == "wifi-source-1");

        Assert.NotNull(provider.LastRequest);
        Assert.Equal("WiFi", provider.LastRequest!.DetectedIntent);
        Assert.Contains(provider.LastRequest.SelectedKnowledgeItems, item => item.SourceId == "wifi-source-1");
    }

    [Fact]
    public async Task OrchestrateAsync_ConflictingWiFiPasswords_RequiresHumanReview()
    {
        var first = new ConversationContextKnowledgeItem(
            "wifi-source-1",
            "Guest Wi-Fi",
            "Network: StayFlowGuest\nPassword: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            10,
            true,
            ["wifi"],
            null);

        var second = new ConversationContextKnowledgeItem(
            "wifi-source-2",
            "Guest Wi-Fi Backup",
            "Network: StayFlowGuest\nPassword: DifferentPassword",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            9,
            true,
            ["wifi"],
            null);

        var context = BuildContext([first, second], [first, second]);
        var orchestrator = BuildOrchestrator(context, new SpyDevelopmentProvider(), selectedItems: [first, second]);

        var result = await orchestrator.OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.GeneratedHostReply
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanReview);
        Assert.Contains("Conflicting approved Wi-Fi information was found", result.Output);
        Assert.DoesNotContain("DifferentPassword", result.Output);
        Assert.Contains(result.Warnings, warning => warning.Code == "ConflictingApprovedKnowledge");
    }

    [Fact]
    public async Task OrchestrateAsync_IdenticalDuplicateWiFiValues_DoNotTriggerConflict()
    {
        var first = new ConversationContextKnowledgeItem(
            "wifi-source-1",
            "Guest Wi-Fi",
            "Password: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            10,
            true,
            ["wifi"],
            null);

        var second = new ConversationContextKnowledgeItem(
            "wifi-source-2",
            "Guest Wi-Fi Duplicate",
            "Password: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            9,
            true,
            ["wifi"],
            null);

        var context = BuildContext([first, second], [first, second]);
        var orchestrator = BuildOrchestrator(context, new SpyDevelopmentProvider(), selectedItems: [first, second]);

        var result = await orchestrator.OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.GeneratedHostReply
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanReview);
        Assert.Contains("DemoStay2026", result.Output);
        Assert.DoesNotContain("Conflicting approved Wi-Fi information was found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OrchestrateAsync_FutureGuestReply_WithGroundedContext_DoesNotForceHumanReview()
    {
        var selected = new ConversationContextKnowledgeItem(
            "wifi-source-1",
            "Guest Wi-Fi",
            "Network: StayFlowGuest\nPassword: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            10,
            true,
            ["wifi", "network"],
            "Guest wireless details");

        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider();
        var orchestrator = BuildOrchestrator(context, provider, selectedItems: [selected]);

        var result = await orchestrator.OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.FutureGuestReply,
            RequestedTone = "friendly"
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AIReplyOperation.FutureGuestReply, result!.Operation);
        Assert.False(result.RequiresHumanReview);
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == "FutureGuestReplyNotEnabled");
        Assert.NotNull(result.Output);
    }

    private static AIReplyOrchestrator BuildOrchestrator(
        ConversationContext context,
        SpyDevelopmentProvider provider,
        IReadOnlyCollection<ConversationContextKnowledgeItem> selectedItems)
    {
        return new AIReplyOrchestrator(
            new FakeConversationContextBuilder(context),
            new FakeRanker(selectedItems),
            new AIPromptBuilder(Options.Create(new AIPromptOptions())),
            provider,
            new AIReplyOutputValidator(),
            new AIReplySafetyEvaluator(),
            new ContextConfidenceEvaluator(),
            new AIReplyFallbackProvider(),
            Options.Create(new AIReplyOrchestratorOptions
            {
                EnableFallback = true,
                MaximumSelectedKnowledgeItems = 5,
                MaximumSelectedKnowledgeCharacters = 10000,
                ProviderTimeoutSeconds = 10
            }),
            NullLogger<AIReplyOrchestrator>.Instance);
    }

    private static ConversationContext BuildContext(
        IReadOnlyCollection<ConversationContextKnowledgeItem> approvedKnowledge,
        IReadOnlyCollection<ConversationContextKnowledgeItem> sourceKnowledge)
    {
        var conversationId = Guid.NewGuid();
        var sourceList = new List<ConversationContextSource>
        {
            new(
                ConversationContextSourceType.Conversation,
                null,
                "Conversation",
                null,
                DateTimeOffset.UtcNow,
                "Conversation metadata and visible message history.",
                true),
            new(
                ConversationContextSourceType.Property,
                null,
                "Demo Property",
                "Property",
                DateTimeOffset.UtcNow,
                "Property details are linked to this conversation.",
                true)
        };

        sourceList.AddRange(sourceKnowledge.Select(item => new ConversationContextSource(
            ConversationContextSourceType.PropertyKnowledge,
            item.SourceId,
            item.Title,
            item.Category.ToString(),
            item.LastUpdated,
            "Approved property knowledge available for AI grounding.",
            true)));

        return new ConversationContext(
            conversationId,
            Guid.NewGuid(),
            "Open",
            "Web",
            "Guest question",
            false,
            false,
            "Host",
            "Guest",
            "guest@example.com",
            Guid.NewGuid(),
            "Demo Property",
            Guid.NewGuid(),
            "CONF-123",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 4),
            "Confirmed",
            [
                new ConversationContextVisibleMessage(
                    "m1",
                    "Guest",
                    DateTimeOffset.UtcNow,
                    "What is the Wi-Fi password?")
            ],
            approvedKnowledge,
            sourceList,
            [],
            false,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeConversationContextBuilder(ConversationContext context) : IConversationContextBuilder
    {
        public Task<ConversationContext?> BuildAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult<ConversationContext?>(context);
    }

    private sealed class FakeIntentDetector : IGuestIntentDetector
    {
        public GuestIntentResult Detect(ConversationContext context)
            => new(GuestIntent.WiFi, 0.92, ["wifi", "network"], false, "deterministic");
    }

    private sealed class FakeRanker(IReadOnlyCollection<ConversationContextKnowledgeItem> selectedItems) : IPropertyKnowledgeRanker
    {
        public KnowledgeRetrievalResult Rank(
            ConversationContext context,
            GuestIntentResult intent,
            string latestGuestMessage,
            int maxSelectedItems,
            int maxSelectedCharacters)
        {
            var ranked = selectedItems
                .Select((item, index) => new KnowledgeRetrievalCandidate(
                    item.SourceId,
                    item.Category,
                    100,
                    0.95,
                    ["SelectedInTest"],
                    index + 1,
                    item))
                .ToList();

            return new KnowledgeRetrievalResult(
                intent,
                ranked,
                ranked,
                92,
                KnowledgeConfidenceLevel.High,
                KnowledgeRetrievalReasonCode.StrongKeywordMatch,
                true,
                false,
                false,
                false,
                [],
                ["Selected in test ranker."]);
        }
    }

    private sealed class SpyDevelopmentProvider : IAIProvider
    {
        private readonly DevelopmentAIProvider inner = new();

        public AIProviderRequest? LastRequest { get; private set; }

        public async Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return await inner.GenerateAsync(request, cancellationToken);
        }
    }
}
