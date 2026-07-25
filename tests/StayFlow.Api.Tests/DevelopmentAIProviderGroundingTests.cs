using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class DevelopmentAIProviderGroundingTests
{
    private readonly DevelopmentAIProvider provider = new();

    [Theory]
    [InlineData(QuestionContextCategory.CheckIn, "Check-in code: 4821. For your arrival, check-in code is 4821.", "4821")]
    [InlineData(QuestionContextCategory.Parking, "Parking code: P-7788. Backup note: parking code is P-7788.", "P-7788")]
    [InlineData(QuestionContextCategory.General, "Gate code: 9012. Please remember the gate code is 9012.", "9012")]
    public async Task GenerateAsync_GroundedStructuredFacts_EmitSingleValueOccurrence(QuestionContextCategory category, string content, string value)
    {
        var result = await provider.GenerateAsync(new AIProviderRequest
        {
            QuestionCategories = [category],
            SelectedKnowledgeItems =
            [
                new AIProviderKnowledgeItem
                {
                    SourceId = "k-1",
                    Title = "Structured guidance",
                    Category = category.ToString(),
                    Content = content,
                    IsApproved = true,
                    Priority = 10
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Success, result.Outcome);
        Assert.NotNull(result.ResponseText);
        Assert.Equal(1, CountOccurrences(result.ResponseText!, value));
    }

    [Fact]
    public async Task GenerateAsync_WiFi_ExtractsNetworkAndPassword()
    {
        var result = await provider.GenerateAsync(new AIProviderRequest
        {
            QuestionCategories = [QuestionContextCategory.WiFi],
            DetectedIntent = "WiFi",
            SelectedKnowledgeItems =
            [
                new AIProviderKnowledgeItem
                {
                    SourceId = "wifi-1",
                    Title = "Guest Wi-Fi",
                    Category = "WiFi",
                    Content = "Network: StayFlowGuest\nPassword: DemoStay2026",
                    IsApproved = true
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Success, result.Outcome);
        Assert.Equal("The guest Wi-Fi network is StayFlowGuest, and the password is DemoStay2026.", result.ResponseText);
    }

    [Fact]
    public async Task GenerateAsync_WiFi_InlineNetworkAndPassword_DoesNotDuplicatePassword()
    {
        var result = await provider.GenerateAsync(new AIProviderRequest
        {
            QuestionCategories = [QuestionContextCategory.WiFi],
            SelectedKnowledgeItems =
            [
                new AIProviderKnowledgeItem
                {
                    SourceId = "wifi-1",
                    Title = "Guest Wi-Fi",
                    Category = "WiFi",
                    Content = "Network: StayFlowGuest, Password: DemoStay2026",
                    IsApproved = true
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Success, result.Outcome);
        Assert.Equal("The guest Wi-Fi network is StayFlowGuest, and the password is DemoStay2026.", result.ResponseText);
        Assert.NotNull(result.ResponseText);
        Assert.Equal(1, CountOccurrences(result.ResponseText!, "DemoStay2026"));
    }

    [Fact]
    public async Task GenerateAsync_WiFi_SupportsEqualsAndCaseInsensitiveKeys()
    {
        var result = await provider.GenerateAsync(new AIProviderRequest
        {
            QuestionCategories = [QuestionContextCategory.WiFi],
            SelectedKnowledgeItems =
            [
                new AIProviderKnowledgeItem
                {
                    SourceId = "wifi-1",
                    Title = "Wi-Fi",
                    Category = "WiFi",
                    Content = "NETWORK NAME = StayFlowGuest\nPASSCODE = DemoStay2026",
                    IsApproved = true
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Success, result.Outcome);
        Assert.Contains("StayFlowGuest", result.ResponseText);
        Assert.Contains("DemoStay2026", result.ResponseText);
    }

    [Fact]
    public async Task GenerateAsync_WiFi_PasswordOnly_ReturnsPasswordReply()
    {
        var result = await provider.GenerateAsync(new AIProviderRequest
        {
            QuestionCategories = [QuestionContextCategory.WiFi],
            SelectedKnowledgeItems =
            [
                new AIProviderKnowledgeItem
                {
                    SourceId = "wifi-1",
                    Title = "Wi-Fi",
                    Category = "WiFi",
                    Content = "Password: DemoStay2026",
                    IsApproved = true
                }
            ]
        }, CancellationToken.None);

        Assert.Equal("The guest Wi-Fi password is DemoStay2026.", result.ResponseText);
    }

    [Fact]
    public async Task GenerateAsync_WiFi_NetworkOnly_ReturnsVerificationPrompt()
    {
        var result = await provider.GenerateAsync(new AIProviderRequest
        {
            QuestionCategories = [QuestionContextCategory.WiFi],
            SelectedKnowledgeItems =
            [
                new AIProviderKnowledgeItem
                {
                    SourceId = "wifi-1",
                    Title = "Wi-Fi",
                    Category = "WiFi",
                    Content = "SSID: StayFlowGuest",
                    IsApproved = true
                }
            ]
        }, CancellationToken.None);

        Assert.Equal("The guest Wi-Fi network is StayFlowGuest. I'm checking the password details.", result.ResponseText);
    }

    [Fact]
    public async Task GenerateAsync_WiFi_NoValues_UsesSafeFallback()
    {
        var result = await provider.GenerateAsync(new AIProviderRequest
        {
            QuestionCategories = [QuestionContextCategory.WiFi],
            SelectedKnowledgeItems =
            [
                new AIProviderKnowledgeItem
                {
                    SourceId = "wifi-1",
                    Title = "Wi-Fi",
                    Category = "WiFi",
                    Content = "Wi-Fi is available for guests.",
                    IsApproved = true
                }
            ]
        }, CancellationToken.None);

        Assert.Contains("Host verification is required", result.ResponseText);
        Assert.DoesNotContain("DemoStay2026", result.ResponseText);
    }

    [Fact]
    public async Task GenerateAsync_WiFi_ConflictingValues_ReturnsConflictReply()
    {
        var result = await provider.GenerateAsync(new AIProviderRequest
        {
            QuestionCategories = [QuestionContextCategory.WiFi],
            SelectedKnowledgeItems =
            [
                new AIProviderKnowledgeItem
                {
                    SourceId = "wifi-1",
                    Title = "Wi-Fi A",
                    Category = "WiFi",
                    Content = "Password: DemoStay2026",
                    IsApproved = true
                },
                new AIProviderKnowledgeItem
                {
                    SourceId = "wifi-2",
                    Title = "Wi-Fi B",
                    Category = "WiFi",
                    Content = "Password: DifferentPassword",
                    IsApproved = true
                }
            ]
        }, CancellationToken.None);

        Assert.Contains("Conflicting approved Wi-Fi information was found", result.ResponseText);
        Assert.DoesNotContain("DifferentPassword", result.ResponseText);
    }

    [Fact]
    public async Task GenerateAsync_WiFi_IdenticalDuplicates_DoNotTriggerConflict()
    {
        var result = await provider.GenerateAsync(new AIProviderRequest
        {
            QuestionCategories = [QuestionContextCategory.WiFi],
            SelectedKnowledgeItems =
            [
                new AIProviderKnowledgeItem
                {
                    SourceId = "wifi-1",
                    Title = "Wi-Fi A",
                    Category = "WiFi",
                    Content = "Password: DemoStay2026",
                    IsApproved = true
                },
                new AIProviderKnowledgeItem
                {
                    SourceId = "wifi-2",
                    Title = "Wi-Fi B",
                    Category = "WiFi",
                    Content = "Password: DemoStay2026",
                    IsApproved = true
                }
            ]
        }, CancellationToken.None);

        Assert.Contains("The guest Wi-Fi password is DemoStay2026.", result.ResponseText);
    }

    private static int CountOccurrences(string text, string token)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while (true)
        {
            index = text.IndexOf(token, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            index += token.Length;
        }
    }
}
