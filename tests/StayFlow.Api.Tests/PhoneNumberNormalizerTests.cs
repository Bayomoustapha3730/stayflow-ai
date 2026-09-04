using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class PhoneNumberNormalizerTests
{
    private readonly PhoneNumberNormalizer normalizer = new();

    [Theory]
    [InlineData("+1 (415) 555-1234", "+14155551234")]
    [InlineData(" +254 700 000 002 ", "+254700000002")]
    [InlineData("+44-20-7946-0958", "+442079460958")]
    public void TryNormalize_FormatsValidNumbers(string input, string expected)
    {
        var success = normalizer.TryNormalize(input, out var normalized);

        Assert.True(success);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("0700000002")]
    [InlineData("abc")]
    [InlineData("+12")]
    public void TryNormalize_RejectsInvalidNumbers(string input)
    {
        var success = normalizer.TryNormalize(input, out _);

        Assert.False(success);
    }

    [Theory]
    [InlineData("14155551234")]
    [InlineData("254700000002")]
    [InlineData("01234567890")]
    [InlineData("415 555 1234")]
    [InlineData("(415) 555-1234")]
    [InlineData("1415555123a")]
    public void TryNormalize_RequiresExplicitInternationalPrefix(string input)
    {
        var success = normalizer.TryNormalize(input, out _);

        Assert.False(success);
    }

    [Fact]
    public void Mask_PreservesCountryPrefixAndFinalDigits()
    {
        var masked = normalizer.Mask("+14155551234");

        Assert.Equal("+1******1234", masked);
    }
}