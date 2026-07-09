using StayFlow.Api.DTOs.AIPrompt;

namespace StayFlow.Api.Services;

public interface IAIPromptBuilder
{
    AIPromptPackage Build(AIPromptBuildRequest request);
}
