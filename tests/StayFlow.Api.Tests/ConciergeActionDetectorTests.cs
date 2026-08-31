using StayFlow.Api.Models;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Tests;

public sealed class ConciergeActionDetectorTests
{
    private readonly ConciergeActionDetector detector = new();

    [Theory]
    [InlineData("Can I check in at noon?", ConciergeActionType.RequestEarlyCheckIn)]
    [InlineData("Can I leave at 1 PM?", ConciergeActionType.RequestLateCheckout)]
    [InlineData("The sink is leaking.", ConciergeActionType.CreateMaintenanceTicket)]
    [InlineData("Can I get two towels?", ConciergeActionType.RequestExtraItem)]
    [InlineData("Please clean the room tomorrow.", ConciergeActionType.RequestHousekeeping)]
    [InlineData("I need parking for two cars.", ConciergeActionType.RequestParking)]
    [InlineData("Send me an M-PESA request so I can pay the balance.", ConciergeActionType.RequestPayment)]
    [InlineData("Tell the host I'll arrive late.", ConciergeActionType.NotifyHost)]
    public void Detect_ExplicitRequests_MapToExpectedActions(string message, ConciergeActionType expected)
    {
        var result = detector.Detect(BuildConversation(), message, null, false);

        Assert.Equal(expected, result.ActionType);
        Assert.Equal(ConciergeActionConfidenceLevel.High, result.ConfidenceLevel);
        Assert.False(result.RequiresClarification);
    }

    [Theory]
    [InlineData("What time is check-in?")]
    [InlineData("What time is checkout?")]
    [InlineData("Is late checkout available?")]
    public void Detect_InformationalQuestions_DoNotCreateActions(string message)
    {
        var result = detector.Detect(BuildConversation(), message, null, false);

        Assert.Equal(ConciergeActionType.None, result.ActionType);
        Assert.Equal(ConciergeActionConfidenceLevel.None, result.ConfidenceLevel);
    }

    [Fact]
    public void Detect_MissingVehicleCount_RequestsClarification()
    {
        var result = detector.Detect(BuildConversation(), "I need parking", null, false);

        Assert.Equal(ConciergeActionType.RequestParking, result.ActionType);
        Assert.True(result.RequiresClarification);
        Assert.Contains("VehicleCount", result.MissingRequiredParameters);
    }

    private static Conversation BuildConversation()
    {
        return new Conversation
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            ReservationId = Guid.NewGuid(),
            Status = ConversationStatus.Open,
            Channel = DTOs.ReservationContext.GuestChannel.Web
        };
    }
}
