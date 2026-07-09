namespace StayFlow.Api.DTOs.AIPrompt;

public sealed class AIPromptPackage
{
    public string SystemInstructions { get; init; } = string.Empty;
    public IReadOnlyCollection<AIPromptContextSection> ContextSections { get; init; } = [];
    public string GuestMessage { get; init; } = string.Empty;
    public string PreferredLanguage { get; init; } = "en";
    public IReadOnlyCollection<string> SafetyDirectives { get; init; } = [];
    public AIResponseConstraints ResponseConstraints { get; init; } = new();
    public IReadOnlyCollection<AIPromptMessage> RenderedMessages { get; init; } = [];
}
