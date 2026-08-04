namespace StayFlow.Api.Services.HostCopilot;

public sealed class HostCopilotOptions
{
    public const string SectionName = "HostCopilot";

    public bool EnableLlmWording { get; init; }
    public int NormalPrioritySlaMinutes { get; init; } = 20;
    public int HighPrioritySlaMinutes { get; init; } = 10;
    public int UrgentPrioritySlaMinutes { get; init; } = 4;
    public int MaximumTimelineEvents { get; init; } = 12;
}
