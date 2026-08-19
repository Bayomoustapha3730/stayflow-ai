using StayFlow.Api.DTOs.Payments;
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
            new ReservationPaymentGroundingDto
            {
                ReservationId = Guid.NewGuid(),
                BookingAmount = 3500m,
                Currency = "KES",
                TotalPaid = 2500m,
                RemainingBalance = 1000m,
                HasSuccessfulPayment = true,
                PaymentCount = 2,
                LatestPaymentStatus = "Paid",
                LatestPaymentAmount = 2500m,
                LatestPaymentRequestedAtUtc = DateTimeOffset.UtcNow,
                LatestPaymentCompletedAtUtc = DateTimeOffset.UtcNow,
                LatestProvider = "M-PESA",
                LatestPaymentMethod = "STKPush",
                LatestReceiptNumber = "MPESA-123",
                LatestFailureMessage = null
            },
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
        Assert.Contains("Reservation payment snapshot", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("KES", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("article-1", result.SourceArticleIds);
        Assert.True(result.KnowledgeCharacters > 0);
        Assert.Contains("NoWarnings", result.WarningCodes, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Processing")]
    public void Build_PendingOrProcessingPayment_IsNotRepresentedAsSuccessful(string status)
    {
        var builder = new ConciergePromptBuilder();
        var request = BuildRequest(new ReservationPaymentGroundingDto
        {
            ReservationId = Guid.NewGuid(),
            BookingAmount = 3500m,
            Currency = "KES",
            TotalPaid = 0m,
            RemainingBalance = 3500m,
            HasSuccessfulPayment = false,
            PaymentCount = 1,
            LatestPaymentStatus = status,
            LatestPaymentAmount = 3500m
        });

        var result = builder.Build(request);

        Assert.Contains("Successful payment recorded: No", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"Latest payment status: {status}", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Latest receipt number", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    [InlineData("Expired")]
    public void Build_FailedCancelledOrExpiredPayment_IsAccuratelyGrounded(string status)
    {
        var builder = new ConciergePromptBuilder();
        var request = BuildRequest(new ReservationPaymentGroundingDto
        {
            ReservationId = Guid.NewGuid(),
            BookingAmount = 3500m,
            Currency = "KES",
            TotalPaid = 0m,
            RemainingBalance = 3500m,
            HasSuccessfulPayment = false,
            PaymentCount = 1,
            LatestPaymentStatus = status,
            LatestPaymentAmount = 3500m,
            LatestFailureMessage = status == "Failed" ? "Insufficient funds" : null
        });

        var result = builder.Build(request);

        Assert.Contains($"Latest payment status: {status}", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Successful payment recorded: No", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            status == "Failed",
            result.UserPrompt.Contains("Latest failure message", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_ReceiptNumber_OmittedWhenNotAvailable()
    {
        var builder = new ConciergePromptBuilder();
        var request = BuildRequest(new ReservationPaymentGroundingDto
        {
            ReservationId = Guid.NewGuid(),
            BookingAmount = 3500m,
            Currency = "KES",
            TotalPaid = 0m,
            RemainingBalance = 3500m,
            HasSuccessfulPayment = false,
            PaymentCount = 0
        });

        var result = builder.Build(request);

        Assert.DoesNotContain("Latest receipt number", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_RemainingBalance_IsGroundedAccurately()
    {
        var builder = new ConciergePromptBuilder();
        var request = BuildRequest(new ReservationPaymentGroundingDto
        {
            ReservationId = Guid.NewGuid(),
            BookingAmount = 3500m,
            Currency = "KES",
            TotalPaid = 1250m,
            RemainingBalance = 2250m,
            HasSuccessfulPayment = true,
            PaymentCount = 1,
            LatestPaymentStatus = "Paid",
            LatestReceiptNumber = "MPESA-999"
        });

        var result = builder.Build(request);

        Assert.Contains("Remaining balance: 2250.00 KES", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Latest receipt number: MPESA-999", result.UserPrompt, StringComparison.OrdinalIgnoreCase);
    }

    private static ConciergeLanguageModelRequest BuildRequest(ReservationPaymentGroundingDto paymentGrounding)
    {
        var intent = new ConversationIntentResult(
            GuestIntent.CheckIn,
            [],
            0.86,
            ConversationIntentConfidenceLevel.High,
            ["checkin"],
            false,
            [],
            "checkin");

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

        return new ConciergeLanguageModelRequest(
            "What is my payment status?",
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
            paymentGrounding,
            ConciergeTone.Warm,
            false,
            false,
            false,
            "v1",
            900,
            5000);
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
