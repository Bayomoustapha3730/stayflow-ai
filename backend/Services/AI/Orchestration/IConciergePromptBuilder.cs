namespace StayFlow.Api.Services.AI.Orchestration;

public interface IConciergePromptBuilder
{
    ConciergePromptBuildResult Build(ConciergeLanguageModelRequest request);
}