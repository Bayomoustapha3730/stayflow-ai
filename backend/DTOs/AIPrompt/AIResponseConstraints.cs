namespace StayFlow.Api.DTOs.AIPrompt;

public sealed class AIResponseConstraints
{
    public string PreferredLanguage { get; init; } = "en";
    public int MaxResponseCharacters { get; init; }
    public bool RequiresEscalationWhenInsufficient { get; init; } = true;
    public bool PropertyAccessRestricted { get; init; }
    public bool AllowMarkdown { get; init; }
    public bool GuestFriendlyTone { get; init; } = true;
}
