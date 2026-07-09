namespace StayFlow.Api.DTOs.AIPrompt;

public sealed class AIPromptMessage
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}
