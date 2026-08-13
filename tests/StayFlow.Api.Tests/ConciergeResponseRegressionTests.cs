using Microsoft.Extensions.Options;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Memory;
using StayFlow.Api.Services.AI.Orchestration;
using StayFlow.Api.Services.AI.Retrieval;

namespace StayFlow.Api.Tests;

public sealed class ConciergeResponseRegressionTests
{
    [Fact]
    public void Retrieve_ParkingIntent_DoesNotSelectOffIntentWifiKnowledge()
    {
        var recognizer = new ConversationIntentRecognizer();
        var retriever = BuildRetriever();

        var parking = Item("Parking", PropertyKnowledgeCategory.Parking, "Parking is in Garage B.", "parking,garage", 10);
        var wifi = Item("Wi-Fi", PropertyKnowledgeCategory.WiFi, "Network StayFlowGuest password DemoStay2026", "wifi,password", 10);
        var context = BuildContext([parking, wifi], "Is parking free?");
        var memory = new ConversationMemoryService(recognizer).BuildContext(context, 10, 2500);
        var intent = recognizer.Recognize("Is parking free?", ["Parking"], 3);

        var result = retriever.Retrieve(
            context,
            new KnowledgeRetrievalRequest(Guid.NewGuid(), context.PropertyId, context.ConversationId, "Is parking free?", intent, memory, 8, 3, 3000));

        Assert.NotEmpty(result.SelectedItems);
        Assert.All(result.SelectedItems, item => Assert.Equal(PropertyKnowledgeCategory.Parking, item.Category));
    }

    [Fact]
    public void Generate_ParkingPriceFollowUp_StatesMissingAttributeAndOffersHostHelp()
    {
        var generator = new ConciergeResponseGenerator();
        var intent = new ConversationIntentResult(
            GuestIntent.Parking,
            [],
            0.84,
            ConversationIntentConfidenceLevel.High,
            ["parking"],
            false,
            [],
            "is parking free");

        var selected = Candidate("parking-1", PropertyKnowledgeCategory.Parking, "Parking is available in Garage B.");
        var retrieval = new KnowledgeRetrievalResult(
            intent.ToGuestIntentResult(),
            [selected],
            [selected],
            0.82,
            KnowledgeConfidenceLevel.High,
            KnowledgeRetrievalReasonCode.StrongIntentMatch,
            true,
            false,
            false,
            false,
            [],
            []);

        var result = generator.Generate(new ConciergeResponseRequest(
            "Is parking free?",
            intent,
            retrieval,
            EmptyMemory(),
            "Demo Property",
            null,
            ConciergeTone.Warm,
            "en",
            false));

        Assert.Contains("Garage B", result.Text);
        Assert.Contains("do not have confirmed parking pricing details", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ask the host", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_ExplicitMultiIntent_DoesNotReturnClarification()
    {
        var generator = new ConciergeResponseGenerator();
        var intent = new ConversationIntentResult(
            GuestIntent.WiFi,
            [GuestIntent.Checkout],
            0.64,
            ConversationIntentConfidenceLevel.Medium,
            ["wifi", "checkout"],
            false,
            ["Wi-Fi access", "checkout details"],
            "wifi and checkout");

        var wifi = Candidate("wifi-1", PropertyKnowledgeCategory.WiFi, "The Wi-Fi network is StayFlowGuest and password is DemoStay2026.");
        var checkout = Candidate("checkout-1", PropertyKnowledgeCategory.Checkout, "Checkout is at 11:00 AM.");

        var retrieval = new KnowledgeRetrievalResult(
            intent.ToGuestIntentResult(),
            [wifi, checkout],
            [wifi, checkout],
            0.7,
            KnowledgeConfidenceLevel.Medium,
            KnowledgeRetrievalReasonCode.StrongIntentMatch,
            true,
            false,
            true,
            false,
            ["Wi-Fi access", "checkout details"],
            []);

        var result = generator.Generate(new ConciergeResponseRequest(
            "What is the Wi-Fi password and when is checkout?",
            intent,
            retrieval,
            EmptyMemory(),
            "Demo Property",
            null,
            ConciergeTone.Warm,
            "en",
            false));

        Assert.Equal(ConciergeResponseOutcome.Answered, result.Outcome);
        Assert.False(result.RequiresClarification);
        Assert.Contains("StayFlowGuest", result.Text);
        Assert.Contains("11:00 AM", result.Text);
    }

    private static IPropertyKnowledgeRetriever BuildRetriever()
    {
        return new PropertyKnowledgeRetriever(
            new PropertyKnowledgeRanker(
                Options.Create(new KnowledgeRetrievalOptions()),
                new DeterministicKnowledgeSimilarityScorer()),
            new DeterministicKnowledgeReranker(Options.Create(new KnowledgeRerankerOptions())),
            new KnowledgeQueryExpander(),
            new DeterministicKnowledgeSemanticSimilarityService(
                new NoOpKnowledgeEmbeddingProvider(),
                Options.Create(new KnowledgeEmbeddingOptions())),
            new DeterministicKnowledgeSimilarityScorer(),
            Options.Create(new ConciergeIntelligenceOptions()));
    }

    private static ConversationContext BuildContext(IReadOnlyCollection<ConversationContextKnowledgeItem> items, string latestGuestMessage)
    {
        var sources = items.Select(item => new ConversationContextSource(
            ConversationContextSourceType.PropertyKnowledge,
            item.SourceId,
            item.Title,
            item.Category.ToString(),
            item.LastUpdated,
            "Approved property knowledge available for AI grounding.",
            true)).ToList();

        return new ConversationContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Open",
            "Web",
            "subject",
            false,
            false,
            null,
            "Guest",
            "guest@example.com",
            Guid.NewGuid(),
            "Demo Property",
            Guid.NewGuid(),
            "CONF-001",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 4),
            "Confirmed",
            [new ConversationContextVisibleMessage("m1", "Guest", DateTimeOffset.UtcNow, latestGuestMessage)],
            items,
            sources,
            [],
            false,
            DateTimeOffset.UtcNow);
    }

    private static ConversationContextKnowledgeItem Item(string title, PropertyKnowledgeCategory category, string content, string tags, int priority)
    {
        return new ConversationContextKnowledgeItem(
            Guid.NewGuid().ToString("N"),
            title,
            content,
            category,
            DateTimeOffset.UtcNow,
            priority,
            true,
            tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            null);
    }

    private static KnowledgeRetrievalCandidate Candidate(string articleId, PropertyKnowledgeCategory category, string content)
    {
        var item = new ConversationContextKnowledgeItem(
            articleId,
            articleId,
            content,
            category,
            DateTimeOffset.UtcNow,
            9,
            true,
            [],
            null);

        return new KnowledgeRetrievalCandidate(articleId, category, 0.85, 0.8, ["test"], 1, item)
        {
            LexicalScore = 0.8,
            SemanticScore = 0.8,
            IntentScore = 0.9,
            PriorityScore = 0.9,
            FinalScore = 0.85
        };
    }

    private static ConversationMemoryContext EmptyMemory()
    {
        return new ConversationMemoryContext([], [], null, null, [], null, null, new Dictionary<string, string>(StringComparer.Ordinal), string.Empty, false, DateTimeOffset.UtcNow);
    }
}