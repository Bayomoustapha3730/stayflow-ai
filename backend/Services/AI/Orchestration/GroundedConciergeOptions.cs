namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class GroundedConciergeOptions
{
    public const string SectionName = "AI:GroundedConcierge";

    public bool Enabled { get; init; } = true;
    public string PromptPolicyVersion { get; init; } = "v1";
    public int ProviderTimeoutSeconds { get; init; } = 12;
    public int MaximumResponseCharacters { get; init; } = 1200;
    public int MaximumKnowledgeCharacters { get; init; } = 12000;
    public bool AllowDevelopmentPromptEchoDiagnostics { get; init; }
}