using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Memory;
using StayFlow.Api.Services.AI.Orchestration;
using StayFlow.Api.Services.AI.Retrieval;

namespace StayFlow.Api.Tests;

public sealed class ConciergePromptBuilderTests
{
    [Fact]
    public void Build_UsesApprovedFactsAndIncludesOutcomePolicy()
    {
        var builder = new ConciergePromptBuilder();
        var intent = new ConversationIntentResult(
            GuestIntent.CheckIn,
            [],
            0.86,
            ConversationIntentConfidenceLevel.High,
            ["checkin"],
            false,
            [],
            "checkin");

        var knowledge = new ConversationContextKnowledgeItem(
            "article-1",
            "Check-in policy",
            "Check-in is available from 3:00 PM.",
            PropertyKnowledgeCategory.CheckIn,
            DateTimeOffset.UtcNow,
            10,
            true,
            ["checkin"],
            "Check-in details");

        var retrieval = new KnowledgeRetrievalResult(
            intent.ToGuestIntentResult(),
            [Candidate("article-1", PropertyKnowledgeCategory.CheckIn, "Check-in is available from 3:00 PM.")],
            [Candidate("article-1", PropertyKnowledgeCategory.CheckIn, "Check-in is available from 3:00 PM.")],
            0.82,
            KnowledgeConfidenceLevel.High,
            KnowledgeRetrievalReasonCode.StrongIntentMatch,
            false,
            false,
            false,
            false,
            [],
            []);

        var request = new ConciergeLanguageModelRequest(
            "What time is check-in?",
            intent,
            retrieval,
            new ConversationMemoryContext(
                ["Hi"],
                [],
                GuestIntent.CheckIn,
                "check-in",
                [],
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                "The guest asked about check-in.",
                false,
                DateTimeOffset.UtcNow),
            ConciergeRequiredOutcome.GroundedAnswer,
            "en",
            "Demo Property",
            "Confirmation 123",
            ConciergeTone.Warm,
            false,
            false,
            false,
            "v1",
            900,
            5000);

        var result = builder.Build(request);

        Assert.Contains("StayFlow Concierge", result.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GroundedAnswer", result.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("What time is check-in?", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Check-in is available from 3:00 PM.", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("article-1", result.SourceArticleIds);
        Assert.True(result.KnowledgeCharacters > 0);
        Assert.Contains("NoWarnings", result.WarningCodes, StringComparer.OrdinalIgnoreCase);
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
            FinalScore = 0.85
        };
    }
}
