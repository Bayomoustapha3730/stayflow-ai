using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Tests;

public sealed class AIPromptBuilderReplyTests
{
    [Fact]
    public void BuildReply_IncludesSelectedKnowledgeContentAndMetadata()
    {
        var builder = new AIPromptBuilder(Options.Create(new AIPromptOptions()));
        var prompt = builder.BuildReply(new AIReplyPromptBuildRequest
        {
            ConversationContext = Context(),
            Intent = new GuestIntentResult(GuestIntent.WiFi, 0.92, ["wifi"], false, "deterministic"),
            SelectedKnowledgeItems =
            [
                new ConversationContextKnowledgeItem(
                    "k1",
                    "Guest Wi-Fi",
                    "Network: StayFlowGuest\nPassword: DemoStay2026",
                    PropertyKnowledgeCategory.WiFi,
                    DateTimeOffset.UtcNow,
                    10,
                    true,
                    ["wifi", "network"],
                    "Guest wireless details")
            ],
            Operation = AIReplyOperation.GeneratedHostReply,
            RequestedTone = "professional"
        });

        var userContent = prompt.RenderedMessages.Single(message => message.Role == "user").Content;

        Assert.Contains("APPROVED PROPERTY KNOWLEDGE", userContent);
        Assert.Contains("[Source 1]", userContent);
        Assert.Contains("- Property: Demo Property", userContent);
        Assert.Contains("Title: Guest Wi-Fi", userContent);
        Assert.Contains("Category: WiFi", userContent);
        Assert.Contains("Tags: wifi, network", userContent);
        Assert.Contains("Summary: Guest wireless details", userContent);
        Assert.Contains("Network: StayFlowGuest", userContent);
        Assert.Contains("Password: DemoStay2026", userContent);
    }

    [Fact]
    public void BuildReply_LabelsConversationTextAsUntrusted()
    {
        var builder = new AIPromptBuilder(Options.Create(new AIPromptOptions()));
        var prompt = builder.BuildReply(new AIReplyPromptBuildRequest
        {
            ConversationContext = Context(),
            Intent = new GuestIntentResult(GuestIntent.WiFi, 0.92, ["wifi"], false, "deterministic"),
            SelectedKnowledgeItems = [],
            Operation = AIReplyOperation.GeneratedHostReply
        });

        var userContent = prompt.RenderedMessages.Single(message => message.Role == "user").Content;
        var developerContent = prompt.RenderedMessages.Single(message => message.Role == "developer").Content;

        Assert.Contains("UNTRUSTED CONVERSATION TEXT", userContent);
        Assert.Contains("When approved property knowledge directly answers the guest question", developerContent);
        Assert.Contains("Do not invent missing values", developerContent);
    }

    [Fact]
    public void BuildReply_IncludesTenantDerivedReservationContext()
    {
        var builder = new AIPromptBuilder(Options.Create(new AIPromptOptions()));
        var prompt = builder.BuildReply(new AIReplyPromptBuildRequest
        {
            ConversationContext = Context(),
            Intent = new GuestIntentResult(GuestIntent.CheckIn, 0.95, ["check-in"], false, "deterministic"),
            SelectedKnowledgeItems = [],
            Operation = AIReplyOperation.GeneratedHostReply
        });

        var userContent = prompt.RenderedMessages.Single(message => message.Role == "user").Content;

        Assert.Contains("Reservation summary:", userContent);
        Assert.Contains("- Guest: Guest", userContent);
        Assert.Contains("- Confirmation: DEMO-123", userContent);
        Assert.Contains("- Check-in: 2026-08-01", userContent);
        Assert.Contains("- Check-out: 2026-08-04", userContent);
        Assert.Contains("- Reservation status: Confirmed", userContent);
    }

    [Fact]
    public void BuildReply_MarksMissingReservationFieldsUnavailableInsteadOfBlank()
    {
        var builder = new AIPromptBuilder(Options.Create(new AIPromptOptions()));
        var prompt = builder.BuildReply(new AIReplyPromptBuildRequest
        {
            ConversationContext = Context() with
            {
                ConfirmationNumber = null,
                CheckInDate = null,
                CheckOutDate = null,
                ReservationStatus = null
            },
            Intent = new GuestIntentResult(GuestIntent.CheckIn, 0.95, ["check-in"], false, "deterministic"),
            SelectedKnowledgeItems = [],
            Operation = AIReplyOperation.GeneratedHostReply
        });

        var userContent = prompt.RenderedMessages.Single(message => message.Role == "user").Content;

        Assert.Contains("- Confirmation: Unavailable", userContent);
        Assert.Contains("- Check-in: Unavailable", userContent);
        Assert.Contains("- Check-out: Unavailable", userContent);
        Assert.Contains("- Reservation status: Unavailable", userContent);
    }

    private static ConversationContext Context()
    {
        return new ConversationContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Open",
            "Web",
            "Question",
            false,
            false,
            "Host",
            "Guest",
            "guest@example.com",
            Guid.NewGuid(),
            "Demo Property",
            Guid.NewGuid(),
            "DEMO-123",
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
            [],
            [],
            [],
            false,
            DateTimeOffset.UtcNow);
    }
}
