namespace StayFlow.Api.Services;

public sealed class AIPromptOptions
{
    public const string SectionName = "AIPrompt";
    public int MaxResponseCharacters { get; init; } = 1200;
    public bool AllowMarkdown { get; init; }
}
