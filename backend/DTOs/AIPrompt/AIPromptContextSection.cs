namespace StayFlow.Api.DTOs.AIPrompt;

public sealed class AIPromptContextSection
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Items { get; init; } = [];
}
