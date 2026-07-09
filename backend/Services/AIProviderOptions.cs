namespace StayFlow.Api.Services;

public sealed class AIProviderOptions
{
    public const string SectionName = "AIProvider";
    public string Provider { get; init; } = "Development";
}
