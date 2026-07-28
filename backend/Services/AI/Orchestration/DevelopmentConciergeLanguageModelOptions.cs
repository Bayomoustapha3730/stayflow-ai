namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class DevelopmentConciergeLanguageModelOptions
{
    public const string SectionName = "AI:GroundedConcierge:DevelopmentLanguageModel";

    public DevelopmentConciergeLanguageModelMode Mode { get; init; } = DevelopmentConciergeLanguageModelMode.Success;
    public int SimulatedLatencyMilliseconds { get; init; } = 5;
}