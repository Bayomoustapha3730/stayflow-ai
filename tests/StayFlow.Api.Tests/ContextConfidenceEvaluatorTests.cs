using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Tests;

public sealed class ContextConfidenceEvaluatorTests
{
    private readonly ContextConfidenceEvaluator evaluator = new();

    [Fact]
    public void Evaluate_FullContext_ReturnsHigh()
    {
        var result = evaluator.Evaluate(Context());

        Assert.Equal(ContextConfidenceLevel.High, result.Level);
        Assert.InRange(result.Score, 80, 100);
    }

    [Fact]
    public void Evaluate_MissingProperty_LowersScore()
    {
        var result = evaluator.Evaluate(Context() with { PropertyId = null, PropertyName = null });

        Assert.True(result.Score <= 70);
        Assert.Contains(ConversationContextWarning.MissingProperty, result.MissingContext);
    }

    [Fact]
    public void Evaluate_MissingReservation_LowersScore()
    {
        var result = evaluator.Evaluate(Context() with { ReservationId = null, ConfirmationNumber = null });

        Assert.True(result.Score <= 80);
        Assert.Contains(ConversationContextWarning.MissingReservation, result.MissingContext);
    }

    [Fact]
    public void Evaluate_NoApprovedKnowledge_LowersScore()
    {
        var result = evaluator.Evaluate(Context() with { ApprovedKnowledgeItems = [] });

        Assert.True(result.Score <= 80);
        Assert.Contains(ConversationContextWarning.NoApprovedKnowledge, result.MissingContext);
    }

    [Fact]
    public void Evaluate_NoVisibleMessages_LowersScore()
    {
        var result = evaluator.Evaluate(Context() with { VisibleMessages = [] });

        Assert.True(result.Score <= 75);
        Assert.Contains(ConversationContextWarning.NoVisibleMessages, result.MissingContext);
    }

    [Fact]
    public void Evaluate_ScoreIsClamped()
    {
        var empty = Context() with
        {
            PropertyId = null,
            ReservationId = null,
            ApprovedKnowledgeItems = [],
            VisibleMessages = [],
            Truncated = true
        };

        var result = evaluator.Evaluate(empty);

        Assert.InRange(result.Score, 0, 100);
    }

    [Fact]
    public void Evaluate_IsDeterministic()
    {
        var context = Context();

        var first = evaluator.Evaluate(context);
        var second = evaluator.Evaluate(context);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.Reasons, second.Reasons);
        Assert.Equal(first.MissingContext, second.MissingContext);
    }

    private static ConversationContext Context()
    {
        return new ConversationContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Open",
            "Web",
            "Question",
            true,
            true,
            "Host A",
            "Guest A",
            "guest@example.com",
            Guid.NewGuid(),
            "Demo Nairobi Apartment",
            Guid.NewGuid(),
            "DEMO-CONF-001",
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)),
            [new ConversationContextVisibleMessage("m1", "Guest", DateTimeOffset.UtcNow, "Can I check in early today?")],
            [new ConversationContextKnowledgeItem("Check-in", "Standard check-in is after 3 PM", PropertyKnowledgeCategory.CheckIn, DateTimeOffset.UtcNow, 0, true)],
            [new ConversationContextSource(ConversationContextSourceType.Property, null, "Demo Nairobi Apartment", "Property", DateTimeOffset.UtcNow, "Property details are linked to this conversation.", true)],
            [],
            false,
            DateTimeOffset.UtcNow);
    }
}
