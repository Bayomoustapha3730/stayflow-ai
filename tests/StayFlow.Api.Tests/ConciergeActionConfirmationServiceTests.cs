using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Tests;

public sealed class ConciergeActionConfirmationServiceTests
{
    private readonly ConciergeActionConfirmationService service = new();

    [Theory]
    [InlineData("yes")]
    [InlineData("confirm")]
    [InlineData("submit it")]
    public void IsAffirmative_RecognizesSupportedConfirmations(string value)
    {
        Assert.True(service.IsAffirmative(value));
    }

    [Theory]
    [InlineData("no")]
    [InlineData("nope")]
    public void IsNegative_RecognizesNegativeReplies(string value)
    {
        Assert.True(service.IsNegative(value));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("never mind")]
    public void IsCancel_RecognizesCancellationReplies(string value)
    {
        Assert.True(service.IsCancel(value));
    }
}
