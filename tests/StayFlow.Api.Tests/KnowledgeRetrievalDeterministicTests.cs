using Microsoft.Extensions.Options;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Retrieval;

namespace StayFlow.Api.Tests;

public sealed class KnowledgeRetrievalDeterministicTests
{
    private readonly GuestIntentDetector detector = new();
    private readonly PropertyKnowledgeRanker ranker = new(
        Options.Create(new KnowledgeRetrievalOptions()),
        new DeterministicKnowledgeSimilarityScorer());

    [Fact]
    public void IntentClassification_SupportsEnglishFrenchAndAmbiguity()
    {
        AssertIntent("What is the Wi-Fi password?", GuestIntent.WiFi);
        AssertIntent("Check-in information", GuestIntent.CheckIn);
        AssertIntent("How do I check out?", GuestIntent.Checkout);
        AssertIntent("Where can I park?", GuestIntent.Parking);
        AssertIntent("What are the house rules?", GuestIntent.HouseRules);
        AssertIntent("Where can I eat nearby?", GuestIntent.LocalRecommendations);
        AssertIntent("There is a fire", GuestIntent.Emergency);
        AssertIntent("Quel est le mot de passe Wi-Fi ?", GuestIntent.WiFi);
        AssertIntent("Quelles sont les instructions d'arrivee ?", GuestIntent.CheckIn);
        AssertIntent("A quelle heure est le depart ?", GuestIntent.Checkout);

        var ambiguousIntent = Detect("Tell me about access");
        Assert.True(ambiguousIntent.Intent is GuestIntent.Access or GuestIntent.PropertyAccess);
        Assert.True(ambiguousIntent.Ambiguous || ambiguousIntent.ConfidenceScore < 0.7);

        var benignEmergency = Detect("I need the emergency contact for check-in");
        Assert.NotEqual(GuestIntent.Emergency, benignEmergency.Intent);
    }

    [Fact]
    public void Ranking_CategoryMatchBeatsHigherUnrelatedPriority()
    {
        var context = BuildContext(
            Item("Emergency guidance", PropertyKnowledgeCategory.Emergency, "Call emergency services if there is immediate danger.", priority: 10),
            Item("Check-in details", PropertyKnowledgeCategory.CheckIn, "Check-in starts at 3 PM.", priority: 9));

        var intent = Detect("Check-in Information");
        var result = ranker.Rank(context, intent, "Check-in Information", 3, 10000);

        Assert.NotEmpty(result.SelectedItems);
        Assert.Equal(PropertyKnowledgeCategory.CheckIn, result.SelectedItems.First().Category);
        Assert.DoesNotContain(result.SelectedItems, item => item.Category == PropertyKnowledgeCategory.Emergency && item.Rank == 1);
    }

    [Fact]
    public void Ranking_ExactTitleAndTagSignalsWinAndPriorityOnlyBreaksTies()
    {
        var context = BuildContext(
            Item("Check-in details", PropertyKnowledgeCategory.CheckIn, "Arrival process", tags: "arrival,entry", priority: 4),
            Item("Arrival FAQ", PropertyKnowledgeCategory.CheckIn, "General notes", tags: "arrival,check in", priority: 10),
            Item("Other note", PropertyKnowledgeCategory.Other, "Check-in details are elsewhere", tags: "misc", priority: 10));

        var result = ranker.Rank(context, Detect("Check-in details"), "Check-in details", 3, 10000);

        Assert.NotEmpty(result.SelectedItems);
        Assert.Equal("Check-in details", result.SelectedItems.First().Item.Title);
        Assert.Contains(result.SelectedItems.First().MatchSignals, signal => signal == nameof(KnowledgeRetrievalReasonCode.ExactTitleMatch));
    }

    [Fact]
    public void Ranking_EmergencyIntentRanksEmergencyFirst()
    {
        var context = BuildContext(
            Item("Check-in details", PropertyKnowledgeCategory.CheckIn, "Check-in starts at 3 PM."),
            Item("Emergency guidance", PropertyKnowledgeCategory.Emergency, "If there is a fire, evacuate and call emergency services."));

        var result = ranker.Rank(context, Detect("There is a fire"), "There is a fire", 3, 10000);

        Assert.NotEmpty(result.SelectedItems);
        Assert.Equal(PropertyKnowledgeCategory.Emergency, result.SelectedItems.First().Category);
        Assert.True(result.ConfidenceLevel is KnowledgeConfidenceLevel.Medium or KnowledgeConfidenceLevel.High);
    }

    [Fact]
    public void Ranking_CheckoutQuestion_DoesNotFallbackToEmergency()
    {
        var context = BuildContext(
            Item("Emergency guidance", PropertyKnowledgeCategory.Emergency, "Fire or danger instructions.", priority: 10),
            Item("Checkout details", PropertyKnowledgeCategory.Checkout, "Checkout is at 11:00 AM and keys must be returned.", tags: "checkout,departure", priority: 2));

        var result = ranker.Rank(context, Detect("What are the checkout instructions?"), "What are the checkout instructions?", 3, 10000);

        Assert.NotEmpty(result.Candidates);
        Assert.DoesNotContain(result.Candidates, candidate => candidate.Category == PropertyKnowledgeCategory.Emergency);
        Assert.Equal(PropertyKnowledgeCategory.Checkout, result.SelectedItems.First().Category);
        Assert.True(result.Candidates.Count <= 5);
    }

    [Fact]
    public void Ranking_UnknownQuestion_ReturnsNoKnowledgeMatch()
    {
        var context = BuildContext(
            Item("Emergency guidance", PropertyKnowledgeCategory.Emergency, "Emergency only instructions", priority: 10),
            Item("Wi-Fi", PropertyKnowledgeCategory.WiFi, "Network and password details", tags: "wifi,password", priority: 9));

        var result = ranker.Rank(context, Detect("What color is the sofa?"), "What color is the sofa?", 3, 10000);

        Assert.Empty(result.SelectedItems);
        Assert.True(result.EscalationRecommended || result.ConfidenceLevel == KnowledgeConfidenceLevel.Low);
        Assert.True(result.Candidates.Count <= 5);
    }

    [Fact]
    public void Ranking_WiFiQuestion_SelectsWiFiNotEmergency()
    {
        var context = BuildContext(
            Item("Emergency guidance", PropertyKnowledgeCategory.Emergency, "Call emergency services for danger.", priority: 10),
            Item("Guest Wi-Fi", PropertyKnowledgeCategory.WiFi, "Network: StayFlowGuest Password: DemoStay2026", tags: "wifi,network,password", priority: 4));

        var result = ranker.Rank(context, Detect("What is the Wi-Fi password?"), "What is the Wi-Fi password?", 3, 10000);

        Assert.NotEmpty(result.SelectedItems);
        Assert.Equal(PropertyKnowledgeCategory.WiFi, result.SelectedItems.First().Category);
        Assert.DoesNotContain(result.Candidates, candidate => candidate.Category == PropertyKnowledgeCategory.Emergency);
        Assert.True(result.Candidates.Count <= 5);
    }

    [Fact]
    public void Ranking_EmergencyQuestion_SelectsEmergencyWhenClearlyUnsafe()
    {
        var context = BuildContext(
            Item("Checkout details", PropertyKnowledgeCategory.Checkout, "Checkout is at 11:00 AM.", priority: 10),
            Item("Emergency guidance", PropertyKnowledgeCategory.Emergency, "If there is a fire, evacuate now and call emergency services.", tags: "fire,emergency", priority: 1));

        var result = ranker.Rank(context, Detect("There is smoke and a fire"), "There is smoke and a fire", 3, 10000);

        Assert.NotEmpty(result.SelectedItems);
        Assert.Equal(PropertyKnowledgeCategory.Emergency, result.SelectedItems.First().Category);
        Assert.True(result.Candidates.Count <= 5);
    }

    [Fact]
    public void RetrievalEvaluationDataset_MeetsAcceptanceGoals()
    {
        var dataset = BuildDataset();
        var context = BuildDefaultContext();

        var top1Correct = 0;
        var top3Hit = 0;
        var noMatchCorrect = 0;
        var emergencyRecallNumerator = 0;
        var emergencyRecallDenominator = 0;
        var emergencyPrecisionNumerator = 0;
        var emergencyPrecisionDenominator = 0;
        var inappropriateEmergencySelections = 0;
        var selectedCountSum = 0;

        foreach (var sample in dataset)
        {
            var intent = Detect(sample.Query);
            var result = ranker.Rank(context, intent, sample.Query, 3, 10000);
            var top = result.SelectedItems.FirstOrDefault();
            var topCategory = top?.Category;

            if (topCategory == sample.ExpectedTopCategory)
            {
                top1Correct++;
            }

            if (result.SelectedItems.Take(3).Any(item => item.Category == sample.ExpectedTopCategory))
            {
                top3Hit++;
            }

            if (sample.ExpectNoMatch)
            {
                if (result.SelectedItems.Count == 0 || result.RequiresClarification || result.EscalationRecommended)
                {
                    noMatchCorrect++;
                }
            }

            if (sample.ExpectedTopCategory == PropertyKnowledgeCategory.Emergency)
            {
                emergencyRecallDenominator++;
                if (topCategory == PropertyKnowledgeCategory.Emergency)
                {
                    emergencyRecallNumerator++;
                }
            }

            if (topCategory == PropertyKnowledgeCategory.Emergency)
            {
                emergencyPrecisionDenominator++;
                if (sample.ExpectedTopCategory == PropertyKnowledgeCategory.Emergency)
                {
                    emergencyPrecisionNumerator++;
                }
                else
                {
                    inappropriateEmergencySelections++;
                }
            }

            selectedCountSum += result.SelectedItems.Count;

            Assert.True(
                intent.Intent == sample.ExpectedIntent || sample.AllowIntentNeighbor && IsNeighborIntent(intent.Intent, sample.ExpectedIntent),
                $"Intent mismatch for query '{sample.Query}'. Expected {sample.ExpectedIntent}, got {intent.Intent}.");
        }

        var top1Accuracy = (double)top1Correct / dataset.Count;
        var top3Recall = (double)top3Hit / dataset.Count;
        var noMatchAccuracy = (double)noMatchCorrect / dataset.Count(sample => sample.ExpectNoMatch);
        var emergencyRecall = emergencyRecallDenominator == 0 ? 1 : (double)emergencyRecallNumerator / emergencyRecallDenominator;
        var emergencyPrecision = emergencyPrecisionDenominator == 0 ? 1 : (double)emergencyPrecisionNumerator / emergencyPrecisionDenominator;
        var averageSelected = (double)selectedCountSum / dataset.Count;

        Assert.True(noMatchAccuracy >= 0.80, $"No-match accuracy {noMatchAccuracy:P2} is below 80%.");
        Assert.True(emergencyRecall >= 0.60, $"Emergency recall {emergencyRecall:P2} is below 60%.");
        Assert.True(emergencyPrecision >= 0.95, $"Emergency precision {emergencyPrecision:P2} is below 95%.");
        Assert.Equal(0, inappropriateEmergencySelections);
        Assert.True(averageSelected <= 2.5, $"Average selected item count {averageSelected:0.00} is too high.");
    }

    private void AssertIntent(string query, GuestIntent expected)
    {
        var detected = Detect(query);
        Assert.Equal(expected, detected.Intent);
    }

    private GuestIntentResult Detect(string query)
    {
        var context = BuildContextWithMessages([new ConversationContextVisibleMessage("m1", "Guest", DateTimeOffset.UtcNow, query)]);
        return detector.Detect(context);
    }

    private static bool IsNeighborIntent(GuestIntent detected, GuestIntent expected)
    {
        return (detected, expected) switch
        {
            (GuestIntent.Access, GuestIntent.CheckIn) => true,
            (GuestIntent.PropertyAccess, GuestIntent.CheckIn) => true,
            (GuestIntent.GeneralProperty, GuestIntent.GeneralQuestion) => true,
            _ => false
        };
    }

    private static ConversationContext BuildDefaultContext()
    {
        return BuildContext(
            Item("Guest Wi-Fi", PropertyKnowledgeCategory.WiFi, "Network: StayFlowGuest\nPassword: DemoStay2026", tags: "wifi,network,password", priority: 10),
            Item("Check-in details", PropertyKnowledgeCategory.CheckIn, "Check-in starts at 3:00 PM. Use smart lock instructions sent on arrival day.", tags: "check in,arrival,entry", priority: 9),
            Item("Checkout details", PropertyKnowledgeCategory.Checkout, "Checkout is at 11:00 AM. Please return keys and lock the door.", tags: "checkout,departure", priority: 9),
            Item("Parking", PropertyKnowledgeCategory.Parking, "Guest parking is in Garage B. Use space 17.", tags: "parking,garage,car", priority: 8),
            Item("House rules", PropertyKnowledgeCategory.HouseRules, "Quiet hours are 10 PM to 8 AM. No parties. No smoking.", tags: "rules,quiet hours,smoking,parties", priority: 8),
            Item("Local recommendations", PropertyKnowledgeCategory.LocalRecommendations, "Nearby options: Java House, Carrefour, Nairobi National Museum.", tags: "restaurant,grocery,nearby,recommendations", priority: 7),
            Item("Amenities", PropertyKnowledgeCategory.Amenities, "The property includes pool access and a small gym.", tags: "amenities,pool,gym", priority: 7),
            Item("Property access", PropertyKnowledgeCategory.CheckIn, "Entry is via smart lock with a one-time code shared before arrival.", tags: "access,entry,door", priority: 8),
            Item("Emergency guidance", PropertyKnowledgeCategory.Emergency, "If there is immediate danger, evacuate and call local emergency services.", tags: "emergency,fire,gas leak,injured", priority: 10));
    }

    private static ConversationContext BuildContext(params ConversationContextKnowledgeItem[] items)
    {
        return BuildContext(items, [new ConversationContextVisibleMessage("m1", "Guest", DateTimeOffset.UtcNow, "help")]);
    }

    private static ConversationContext BuildContextWithMessages(IReadOnlyCollection<ConversationContextVisibleMessage> messages)
    {
        return BuildContext([], messages);
    }

    private static ConversationContext BuildContext(IReadOnlyCollection<ConversationContextKnowledgeItem> items, IReadOnlyCollection<ConversationContextVisibleMessage> messages)
    {
        var sources = items.Select(item => new ConversationContextSource(
            ConversationContextSourceType.PropertyKnowledge,
            item.SourceId,
            item.Title,
            item.Category.ToString(),
            item.LastUpdated,
            "Approved property knowledge available for AI grounding.",
            item.IsApproved)).ToList();

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
            new DateOnly(2026, 8, 4),
            "Confirmed",
            messages,
            items,
            sources,
            [],
            false,
            DateTimeOffset.UtcNow);
    }

    private static ConversationContextKnowledgeItem Item(
        string title,
        PropertyKnowledgeCategory category,
        string content,
        string tags = "",
        int priority = 8,
        bool approved = true)
    {
        return new ConversationContextKnowledgeItem(
            Guid.NewGuid().ToString("N"),
            title,
            content,
            category,
            DateTimeOffset.UtcNow,
            priority,
            approved,
            string.IsNullOrWhiteSpace(tags)
                ? []
                : tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            null);
    }

    private static List<DatasetRow> BuildDataset()
    {
        return
        [
            new("What is the Wi-Fi password?", GuestIntent.WiFi, PropertyKnowledgeCategory.WiFi, false, KnowledgeConfidenceLevel.High),
            new("How do I connect to the internet?", GuestIntent.WiFi, PropertyKnowledgeCategory.WiFi, false, KnowledgeConfidenceLevel.Low),
            new("Quel est le mot de passe Wi-Fi ?", GuestIntent.WiFi, PropertyKnowledgeCategory.WiFi, false, KnowledgeConfidenceLevel.Medium),
            new("Check-in information", GuestIntent.CheckIn, PropertyKnowledgeCategory.CheckIn, false, KnowledgeConfidenceLevel.High),
            new("What time can I arrive?", GuestIntent.CheckIn, PropertyKnowledgeCategory.CheckIn, false, KnowledgeConfidenceLevel.Medium),
            new("How do I get inside?", GuestIntent.CheckIn, PropertyKnowledgeCategory.CheckIn, false, KnowledgeConfidenceLevel.Medium, AllowIntentNeighbor: true),
            new("Quelles sont les instructions d'arrivee ?", GuestIntent.CheckIn, PropertyKnowledgeCategory.CheckIn, false, KnowledgeConfidenceLevel.Medium),
            new("When do I need to leave?", GuestIntent.Checkout, PropertyKnowledgeCategory.Checkout, false, KnowledgeConfidenceLevel.Medium),
            new("What are the checkout instructions?", GuestIntent.Checkout, PropertyKnowledgeCategory.Checkout, false, KnowledgeConfidenceLevel.High),
            new("A quelle heure est le depart ?", GuestIntent.Checkout, PropertyKnowledgeCategory.Checkout, false, KnowledgeConfidenceLevel.Medium),
            new("Where can I park?", GuestIntent.Parking, PropertyKnowledgeCategory.Parking, false, KnowledgeConfidenceLevel.High),
            new("Is there a garage?", GuestIntent.Parking, PropertyKnowledgeCategory.Parking, false, KnowledgeConfidenceLevel.Medium),
            new("Ou puis-je stationner ?", GuestIntent.Parking, PropertyKnowledgeCategory.Parking, false, KnowledgeConfidenceLevel.Medium),
            new("Are parties allowed?", GuestIntent.HouseRules, PropertyKnowledgeCategory.HouseRules, false, KnowledgeConfidenceLevel.Medium),
            new("What are quiet hours?", GuestIntent.HouseRules, PropertyKnowledgeCategory.HouseRules, false, KnowledgeConfidenceLevel.Medium),
            new("Can I smoke?", GuestIntent.HouseRules, PropertyKnowledgeCategory.HouseRules, false, KnowledgeConfidenceLevel.Medium),
            new("What are the house rules?", GuestIntent.HouseRules, PropertyKnowledgeCategory.HouseRules, false, KnowledgeConfidenceLevel.High),
            new("Where can I eat nearby?", GuestIntent.LocalRecommendations, PropertyKnowledgeCategory.LocalRecommendations, false, KnowledgeConfidenceLevel.Medium),
            new("Any local recommendations?", GuestIntent.LocalRecommendations, PropertyKnowledgeCategory.LocalRecommendations, false, KnowledgeConfidenceLevel.Medium),
            new("Is there a grocery store close by?", GuestIntent.LocalRecommendations, PropertyKnowledgeCategory.LocalRecommendations, false, KnowledgeConfidenceLevel.Medium),
            new("There is a fire", GuestIntent.Emergency, PropertyKnowledgeCategory.Emergency, false, KnowledgeConfidenceLevel.High),
            new("I smell gas", GuestIntent.Emergency, PropertyKnowledgeCategory.Emergency, false, KnowledgeConfidenceLevel.High),
            new("Someone is injured", GuestIntent.Emergency, PropertyKnowledgeCategory.Emergency, false, KnowledgeConfidenceLevel.High),
            new("I need emergency help", GuestIntent.Emergency, PropertyKnowledgeCategory.Emergency, false, KnowledgeConfidenceLevel.High),
            new("Tell me about access", GuestIntent.Access, PropertyKnowledgeCategory.CheckIn, true, KnowledgeConfidenceLevel.Low, AllowIntentNeighbor: true),
            new("Can you help me?", GuestIntent.GeneralQuestion, PropertyKnowledgeCategory.Other, true, KnowledgeConfidenceLevel.Low, ExpectNoMatch: true),
            new("What color is the sofa?", GuestIntent.Unknown, PropertyKnowledgeCategory.Other, true, KnowledgeConfidenceLevel.Low, ExpectNoMatch: true),
            new("Can I extend my reservation?", GuestIntent.Unknown, PropertyKnowledgeCategory.Other, true, KnowledgeConfidenceLevel.Low, ExpectNoMatch: true),
            new("How to use the pool?", GuestIntent.Amenities, PropertyKnowledgeCategory.Amenities, false, KnowledgeConfidenceLevel.Medium),
            new("Do you have a gym?", GuestIntent.Amenities, PropertyKnowledgeCategory.Amenities, false, KnowledgeConfidenceLevel.Medium),
            new("What is the entry code?", GuestIntent.Access, PropertyKnowledgeCategory.CheckIn, true, KnowledgeConfidenceLevel.Low, AllowIntentNeighbor: true),
            new("I have noise issues", GuestIntent.HouseRules, PropertyKnowledgeCategory.HouseRules, false, KnowledgeConfidenceLevel.Medium, AllowIntentNeighbor: true),
            new("Is smoking allowed in the apartment?", GuestIntent.HouseRules, PropertyKnowledgeCategory.HouseRules, false, KnowledgeConfidenceLevel.Medium),
            new("Where is the nearest restaurant?", GuestIntent.LocalRecommendations, PropertyKnowledgeCategory.LocalRecommendations, false, KnowledgeConfidenceLevel.Medium),
            new("How do I access the property?", GuestIntent.CheckIn, PropertyKnowledgeCategory.CheckIn, false, KnowledgeConfidenceLevel.Medium, AllowIntentNeighbor: true),
            new("What is departure time?", GuestIntent.Checkout, PropertyKnowledgeCategory.Checkout, false, KnowledgeConfidenceLevel.High),
            new("arrivee", GuestIntent.CheckIn, PropertyKnowledgeCategory.CheckIn, false, KnowledgeConfidenceLevel.Medium),
            new("depart", GuestIntent.Checkout, PropertyKnowledgeCategory.Checkout, false, KnowledgeConfidenceLevel.Medium),
            new("stationnement", GuestIntent.Parking, PropertyKnowledgeCategory.Parking, false, KnowledgeConfidenceLevel.Medium),
            new("reglement", GuestIntent.HouseRules, PropertyKnowledgeCategory.HouseRules, false, KnowledgeConfidenceLevel.Medium),
            new("urgence incendie", GuestIntent.Emergency, PropertyKnowledgeCategory.Emergency, false, KnowledgeConfidenceLevel.High),
            new("restaurant a proximite", GuestIntent.LocalRecommendations, PropertyKnowledgeCategory.LocalRecommendations, false, KnowledgeConfidenceLevel.Medium)
        ];
    }

    private sealed record DatasetRow(
        string Query,
        GuestIntent ExpectedIntent,
        PropertyKnowledgeCategory ExpectedTopCategory,
        bool AllowClarification,
        KnowledgeConfidenceLevel MinimumConfidenceLevel,
        bool AllowIntentNeighbor = false,
        bool ExpectNoMatch = false);
}
