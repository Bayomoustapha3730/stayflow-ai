using Microsoft.Extensions.Options;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Memory;
using StayFlow.Api.Services.AI.Orchestration;
using StayFlow.Api.Services.AI.Retrieval;

namespace StayFlow.Api.Tests;

public sealed class ConciergeIntelligenceEvaluationTests
{
    private readonly ConversationIntentRecognizer intentRecognizer = new();
    private readonly ConversationMemoryService memoryService;
    private readonly IPropertyKnowledgeRetriever retriever;

    public ConciergeIntelligenceEvaluationTests()
    {
        memoryService = new ConversationMemoryService(intentRecognizer);
        var ranker = new PropertyKnowledgeRanker(
            Options.Create(new KnowledgeRetrievalOptions()),
            new DeterministicKnowledgeSimilarityScorer());

        retriever = new PropertyKnowledgeRetriever(
            ranker,
            new DeterministicKnowledgeReranker(Options.Create(new KnowledgeRerankerOptions())),
            new KnowledgeQueryExpander(),
            new DeterministicKnowledgeSemanticSimilarityService(
                new NoOpKnowledgeEmbeddingProvider(),
                Options.Create(new KnowledgeEmbeddingOptions())),
            new DeterministicKnowledgeSimilarityScorer(),
            Options.Create(new ConciergeIntelligenceOptions()));
    }

    [Fact]
    public void ConciergeV2_EvaluationDataset_MeetsDeterministicTargets()
    {
        var rows = BuildDataset();
        Assert.True(rows.Count >= 75, "Dataset must contain at least 75 queries.");

        var context = BuildContext();

        var primaryCorrect = 0;
        var directOperationalTotal = 0;
        var directOperationalCorrect = 0;
        var multiIntentTotal = 0;
        var multiIntentHit = 0;
        var top1Correct = 0;
        var top3Hit = 0;
        var unknownTotal = 0;
        var unknownRejected = 0;
        var emergencyTotal = 0;
        var emergencyDetected = 0;
        var emergencySelected = 0;
        var emergencySelectedCorrect = 0;
        var nonEmergencyEmergencySelections = 0;
        var followUpTotal = 0;
        var followUpCorrect = 0;
        var typoTotal = 0;
        var typoCorrect = 0;
        var selectedArticles = 0;

        foreach (var row in rows)
        {
            var messageHistory = new List<ConversationContextVisibleMessage>
            {
                new("m1", "Guest", DateTimeOffset.UtcNow.AddMinutes(-2), row.PriorMessage ?? row.Query),
                new("m2", "AI", DateTimeOffset.UtcNow.AddMinutes(-1), "Thanks, let me check that for you."),
                new("m3", "Guest", DateTimeOffset.UtcNow, row.Query)
            };

            var rowContext = context with { VisibleMessages = messageHistory };
            var memory = memoryService.BuildContext(rowContext, 10, 2500);
            var intent = intentRecognizer.Recognize(row.Query, memory.ActiveTopic is null ? null : [memory.ActiveTopic], 3);
            var retrieval = retriever.Retrieve(rowContext, new KnowledgeRetrievalRequest(
                Guid.NewGuid(),
                rowContext.PropertyId,
                rowContext.ConversationId,
                row.Query,
                intent,
                memory,
                8,
                3,
                9000));

            if (intent.PrimaryIntent == row.ExpectedPrimaryIntent)
            {
                primaryCorrect++;
            }

            if (row.IsOperationalDirect)
            {
                directOperationalTotal++;
                if (intent.PrimaryIntent == row.ExpectedPrimaryIntent)
                {
                    directOperationalCorrect++;
                }
            }

            if (row.ExpectedSecondaryIntents.Count > 0)
            {
                multiIntentTotal++;
                var covered = row.ExpectedSecondaryIntents.All(expected => intent.SecondaryIntents.Contains(expected));
                if (covered)
                {
                    multiIntentHit++;
                }
            }

            var top = retrieval.SelectedItems.FirstOrDefault();
            var topCategory = top?.Category;
            if (topCategory == row.ExpectedTopCategory)
            {
                top1Correct++;
            }

            if (retrieval.SelectedItems.Take(3).Any(item => item.Category == row.ExpectedTopCategory))
            {
                top3Hit++;
            }

            if (row.ExpectUnknownRejection)
            {
                unknownTotal++;
                if (retrieval.SelectedItems.Count == 0
                    || retrieval.ConfidenceLevel is KnowledgeConfidenceLevel.None or KnowledgeConfidenceLevel.Low
                    || retrieval.RequiresClarification)
                {
                    unknownRejected++;
                }
            }

            if (row.ExpectedPrimaryIntent == GuestIntent.Emergency)
            {
                emergencyTotal++;
                if (intent.PrimaryIntent == GuestIntent.Emergency)
                {
                    emergencyDetected++;
                }
            }

            if (topCategory == PropertyKnowledgeCategory.Emergency)
            {
                emergencySelected++;
                if (row.ExpectedPrimaryIntent == GuestIntent.Emergency)
                {
                    emergencySelectedCorrect++;
                }
                else
                {
                    nonEmergencyEmergencySelections++;
                }
            }

            if (row.RequiresConversationContext)
            {
                followUpTotal++;
                if (intent.PrimaryIntent == row.ExpectedPrimaryIntent)
                {
                    followUpCorrect++;
                }
            }

            if (row.IsTypo)
            {
                typoTotal++;
                if (intent.PrimaryIntent == row.ExpectedPrimaryIntent)
                {
                    typoCorrect++;
                }
            }

            selectedArticles += retrieval.SelectedItems.Count;
        }

        var primaryIntentAccuracy = (double)primaryCorrect / rows.Count;
        var directOperationalAccuracy = directOperationalTotal == 0 ? 1 : (double)directOperationalCorrect / directOperationalTotal;
        var multiIntentRecall = multiIntentTotal == 0 ? 1 : (double)multiIntentHit / multiIntentTotal;
        var top1Accuracy = (double)top1Correct / rows.Count;
        var top3Recall = (double)top3Hit / rows.Count;
        var unknownAccuracy = unknownTotal == 0 ? 1 : (double)unknownRejected / unknownTotal;
        var emergencyRecall = emergencyTotal == 0 ? 1 : (double)emergencyDetected / emergencyTotal;
        var emergencyPrecision = emergencySelected == 0 ? 1 : (double)emergencySelectedCorrect / emergencySelected;
        var followUpAccuracy = followUpTotal == 0 ? 1 : (double)followUpCorrect / followUpTotal;
        var typoAccuracy = typoTotal == 0 ? 1 : (double)typoCorrect / typoTotal;
        var averageSelectedArticleCount = (double)selectedArticles / rows.Count;

        Assert.True(primaryIntentAccuracy >= 0.92, $"Primary intent accuracy {primaryIntentAccuracy:P2} below 92%.");
        Assert.True(directOperationalAccuracy >= 1.0, $"Direct operational intent accuracy {directOperationalAccuracy:P2} below 100%.");
        Assert.True(emergencyRecall >= 1.0, $"Emergency recall {emergencyRecall:P2} below 100%.");
        Assert.True(emergencyPrecision >= 0.97, $"Emergency precision {emergencyPrecision:P2} below 97%.");
        Assert.Equal(0, nonEmergencyEmergencySelections);
        Assert.True(unknownAccuracy >= 0.95, $"Unknown rejection accuracy {unknownAccuracy:P2} below 95%.");
        Assert.True(followUpAccuracy >= 0.90, $"Follow-up intent accuracy {followUpAccuracy:P2} below 90%.");
        Assert.True(top3Recall >= 0.9, $"Top-3 retrieval recall {top3Recall:P2} below 90%.");
        Assert.True(typoAccuracy >= 0.9, $"Typo intent accuracy {typoAccuracy:P2} below 90%.");
        Assert.True(averageSelectedArticleCount <= 3.0, $"Average selected article count {averageSelectedArticleCount:0.00} above 3.0.");
    }

    private static ConversationContext BuildContext()
    {
        var items = new List<ConversationContextKnowledgeItem>
        {
            Item("Guest Wi-Fi", PropertyKnowledgeCategory.WiFi, "The guest Wi-Fi network is StayFlowGuest and the password is DemoStay2026.", "wifi,wireless,network,password"),
            Item("Check-in details", PropertyKnowledgeCategory.CheckIn, "Check-in starts at 3:00 PM. Access instructions are sent on arrival day.", "checkin,arrival,entry,access"),
            Item("Checkout details", PropertyKnowledgeCategory.Checkout, "Checkout time is 11:00 AM.", "checkout,departure,leave"),
            Item("Parking", PropertyKnowledgeCategory.Parking, "Parking is available in Garage B, space 17.", "parking,garage,vehicle"),
            Item("House rules", PropertyKnowledgeCategory.HouseRules, "No smoking and no parties. Quiet hours are 10 PM to 8 AM.", "house rules,smoking,quiet hours,parties"),
            Item("Local recommendations", PropertyKnowledgeCategory.LocalRecommendations, "Nearby options include Java House and Nairobi National Museum.", "restaurant,food nearby,attractions,nearby"),
            Item("Amenities", PropertyKnowledgeCategory.Amenities, "Available amenities include pool and gym.", "amenities,pool,gym"),
            Item("Property access", PropertyKnowledgeCategory.CheckIn, "Enter using the keypad code sent before check-in.", "door code,keypad,access code,front door"),
            Item("Emergency guidance", PropertyKnowledgeCategory.Emergency, "If there is immediate danger, call local emergency services first.", "emergency,fire,smoke,gas leak,ambulance")
        };

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
            "Demo Nairobi Apartment",
            Guid.NewGuid(),
            "CONF-001",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 5),
            [new ConversationContextVisibleMessage("seed", "Guest", DateTimeOffset.UtcNow, "hello")],
            items,
            sources,
            [],
            false,
            DateTimeOffset.UtcNow);
    }

    private static ConversationContextKnowledgeItem Item(string title, PropertyKnowledgeCategory category, string content, string tags)
    {
        return new ConversationContextKnowledgeItem(
            Guid.NewGuid().ToString("N"),
            title,
            content,
            category,
            DateTimeOffset.UtcNow,
            8,
            true,
            tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            null);
    }

    private static List<DatasetRow> BuildDataset()
    {
        return
        [
            // Direct intent
            new("What is the Wi-Fi password?", GuestIntent.WiFi, [], PropertyKnowledgeCategory.WiFi, IsOperationalDirect: true),
            new("Do you have internet?", GuestIntent.WiFi, [], PropertyKnowledgeCategory.WiFi, IsOperationalDirect: true),
            new("What is the wireless password?", GuestIntent.WiFi, [], PropertyKnowledgeCategory.WiFi, IsOperationalDirect: true),
            new("When can I get into the apartment?", GuestIntent.CheckIn, [], PropertyKnowledgeCategory.CheckIn, IsOperationalDirect: true),
            new("What time am I allowed inside?", GuestIntent.CheckIn, [], PropertyKnowledgeCategory.CheckIn, IsOperationalDirect: true),
            new("How do I enter?", GuestIntent.PropertyAccess, [], PropertyKnowledgeCategory.CheckIn, IsOperationalDirect: true),
            new("What time do I check out?", GuestIntent.Checkout, [], PropertyKnowledgeCategory.Checkout, IsOperationalDirect: true),
            new("Where do I park?", GuestIntent.Parking, [], PropertyKnowledgeCategory.Parking, IsOperationalDirect: true),
            new("Can I smoke?", GuestIntent.HouseRules, [], PropertyKnowledgeCategory.HouseRules, IsOperationalDirect: true),
            new("Can I bring pets?", GuestIntent.PetPolicy, [], PropertyKnowledgeCategory.HouseRules, ExpectUnknownRejection: true),

            // French
            new("Quel est le mot de passe Wi-Fi ?", GuestIntent.WiFi, [], PropertyKnowledgeCategory.WiFi, IsOperationalDirect: true),
            new("A quelle heure puis-je arriver ?", GuestIntent.CheckIn, [], PropertyKnowledgeCategory.CheckIn, IsOperationalDirect: true),
            new("Comment entrer dans l appartement ?", GuestIntent.PropertyAccess, [], PropertyKnowledgeCategory.CheckIn, IsOperationalDirect: true),
            new("A quelle heure dois-je partir ?", GuestIntent.Checkout, [], PropertyKnowledgeCategory.Checkout, IsOperationalDirect: true),
            new("Ou puis-je stationner ?", GuestIntent.Parking, [], PropertyKnowledgeCategory.Parking, IsOperationalDirect: true),
            new("Les animaux sont ils permis ?", GuestIntent.PetPolicy, [], PropertyKnowledgeCategory.HouseRules, ExpectUnknownRejection: true),
            new("Il y a un incendie", GuestIntent.Emergency, [], PropertyKnowledgeCategory.Emergency, IsOperationalDirect: true),

            // Typos
            new("wifii", GuestIntent.WiFi, [], PropertyKnowledgeCategory.WiFi, IsTypo: true),
            new("wifi pasword", GuestIntent.WiFi, [], PropertyKnowledgeCategory.WiFi, IsTypo: true),
            new("chek in", GuestIntent.CheckIn, [], PropertyKnowledgeCategory.CheckIn, IsTypo: true),
            new("chekout", GuestIntent.Checkout, [], PropertyKnowledgeCategory.Checkout, IsTypo: true),
            new("houze rules", GuestIntent.HouseRules, [], PropertyKnowledgeCategory.HouseRules, IsTypo: true),
            new("parkin", GuestIntent.Parking, [], PropertyKnowledgeCategory.Parking, IsTypo: true),

            // Multi intent
            new("What is the Wi-Fi password and when is checkout?", GuestIntent.WiFi, [GuestIntent.Checkout], PropertyKnowledgeCategory.WiFi),
            new("Where can I park and how do I get inside?", GuestIntent.Parking, [GuestIntent.PropertyAccess], PropertyKnowledgeCategory.Parking),
            new("What are the house rules and can I bring a dog?", GuestIntent.HouseRules, [GuestIntent.PetPolicy], PropertyKnowledgeCategory.HouseRules),

            // Follow-ups
            new("What about checkout?", GuestIntent.Checkout, [], PropertyKnowledgeCategory.Checkout, RequiresConversationContext: true, PriorMessage: "What time is check-in?"),
            new("Is it free?", GuestIntent.Parking, [], PropertyKnowledgeCategory.Parking, RequiresConversationContext: true, PriorMessage: "Where can I park?"),
            new("What about pets?", GuestIntent.PetPolicy, [], PropertyKnowledgeCategory.HouseRules, RequiresConversationContext: true, PriorMessage: "What are the house rules?", ExpectUnknownRejection: true),
            new("How do I get in?", GuestIntent.PropertyAccess, [], PropertyKnowledgeCategory.CheckIn, RequiresConversationContext: true, PriorMessage: "When can I arrive?"),

            // Unknown
            new("What color are the curtains?", GuestIntent.Unknown, [], PropertyKnowledgeCategory.Other, ExpectUnknownRejection: true),
            new("What brand is the television?", GuestIntent.Unknown, [], PropertyKnowledgeCategory.Other, ExpectUnknownRejection: true),
            new("Who built the apartment?", GuestIntent.Unknown, [], PropertyKnowledgeCategory.Other, ExpectUnknownRejection: true),
            new("Can I buy the furniture?", GuestIntent.Unknown, [], PropertyKnowledgeCategory.Other, ExpectUnknownRejection: true),

            // Emergency
            new("There is a fire.", GuestIntent.Emergency, [], PropertyKnowledgeCategory.Emergency),
            new("I smell gas.", GuestIntent.Emergency, [], PropertyKnowledgeCategory.Emergency),
            new("Someone is injured.", GuestIntent.Emergency, [], PropertyKnowledgeCategory.Emergency),
            new("I need an ambulance.", GuestIntent.Emergency, [], PropertyKnowledgeCategory.Emergency),
            new("There is smoke in the kitchen.", GuestIntent.Emergency, [], PropertyKnowledgeCategory.Emergency),

            // Additional natural variants to exceed 75
            new("Can you share wifi details?", GuestIntent.WiFi, [], PropertyKnowledgeCategory.WiFi),
            new("How to connect online?", GuestIntent.WiFi, [], PropertyKnowledgeCategory.WiFi),
            new("router name and password?", GuestIntent.WiFi, [], PropertyKnowledgeCategory.WiFi),
            new("arrival time please", GuestIntent.CheckIn, [], PropertyKnowledgeCategory.CheckIn),
            new("enter the property instructions", GuestIntent.CheckIn, [], PropertyKnowledgeCategory.CheckIn),
            new("late arrival instructions", GuestIntent.CheckIn, [], PropertyKnowledgeCategory.CheckIn),
            new("departure time please", GuestIntent.Checkout, [], PropertyKnowledgeCategory.Checkout),
            new("when do we vacate", GuestIntent.Checkout, [], PropertyKnowledgeCategory.Checkout),
            new("garage info", GuestIntent.Parking, [], PropertyKnowledgeCategory.Parking),
            new("parking space number", GuestIntent.Parking, [], PropertyKnowledgeCategory.Parking),
            new("quiet hours?", GuestIntent.HouseRules, [], PropertyKnowledgeCategory.HouseRules),
            new("are visitors allowed", GuestIntent.HouseRules, [], PropertyKnowledgeCategory.HouseRules),
            new("party policy", GuestIntent.HouseRules, [], PropertyKnowledgeCategory.HouseRules),
            new("pet policy please", GuestIntent.PetPolicy, [], PropertyKnowledgeCategory.HouseRules, ExpectUnknownRejection: true),
            new("service animal policy", GuestIntent.PetPolicy, [], PropertyKnowledgeCategory.HouseRules, ExpectUnknownRejection: true),
            new("restaurant nearby", GuestIntent.LocalRecommendations, [], PropertyKnowledgeCategory.LocalRecommendations),
            new("grocery nearby", GuestIntent.LocalRecommendations, [], PropertyKnowledgeCategory.LocalRecommendations),
            new("things to do nearby", GuestIntent.LocalRecommendations, [], PropertyKnowledgeCategory.LocalRecommendations),
            new("pool hours", GuestIntent.Amenities, [], PropertyKnowledgeCategory.Amenities),
            new("do you have a gym", GuestIntent.Amenities, [], PropertyKnowledgeCategory.Amenities),
            new("door code", GuestIntent.PropertyAccess, [], PropertyKnowledgeCategory.CheckIn),
            new("keypad access", GuestIntent.PropertyAccess, [], PropertyKnowledgeCategory.CheckIn),
            new("front door access code", GuestIntent.PropertyAccess, [], PropertyKnowledgeCategory.CheckIn),
            new("help me", GuestIntent.Unknown, [], PropertyKnowledgeCategory.Other, ExpectUnknownRejection: true),
            new("question about stay", GuestIntent.GeneralProperty, [], PropertyKnowledgeCategory.Other, ExpectUnknownRejection: true),
            new("payment receipt", GuestIntent.Payment, [], PropertyKnowledgeCategory.Other, ExpectUnknownRejection: true),
            new("contact host", GuestIntent.HostContact, [], PropertyKnowledgeCategory.Other, ExpectUnknownRejection: true),
            new("booking confirmation", GuestIntent.Reservation, [], PropertyKnowledgeCategory.CheckIn),
            new("reservation details", GuestIntent.Reservation, [], PropertyKnowledgeCategory.CheckIn),
            new("checkin and parking", GuestIntent.CheckIn, [GuestIntent.Parking], PropertyKnowledgeCategory.CheckIn),
            new("wifi and house rules", GuestIntent.WiFi, [GuestIntent.HouseRules], PropertyKnowledgeCategory.WiFi),
            new("checkout and parking", GuestIntent.Checkout, [GuestIntent.Parking], PropertyKnowledgeCategory.Checkout),
            new("house rules and wifi", GuestIntent.HouseRules, [GuestIntent.WiFi], PropertyKnowledgeCategory.HouseRules),
            new("urgence", GuestIntent.Emergency, [], PropertyKnowledgeCategory.Emergency),
            new("fuite de gaz", GuestIntent.Emergency, [], PropertyKnowledgeCategory.Emergency),
            new("restaurant a proximite", GuestIntent.LocalRecommendations, [], PropertyKnowledgeCategory.LocalRecommendations),
            new("arrivee", GuestIntent.CheckIn, [], PropertyKnowledgeCategory.CheckIn),
            new("depart", GuestIntent.Checkout, [], PropertyKnowledgeCategory.Checkout),
            new("acces", GuestIntent.PropertyAccess, [], PropertyKnowledgeCategory.CheckIn)
        ];
    }

    private sealed record DatasetRow(
        string Query,
        GuestIntent ExpectedPrimaryIntent,
        IReadOnlyCollection<GuestIntent> ExpectedSecondaryIntents,
        PropertyKnowledgeCategory ExpectedTopCategory,
        bool IsOperationalDirect = false,
        bool ExpectUnknownRejection = false,
        bool RequiresConversationContext = false,
        bool IsTypo = false,
        string? PriorMessage = null);
}
