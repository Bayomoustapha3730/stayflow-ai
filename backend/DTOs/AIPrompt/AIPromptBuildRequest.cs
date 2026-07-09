using StayFlow.Api.DTOs.AIContext;

namespace StayFlow.Api.DTOs.AIPrompt;

public sealed class AIPromptBuildRequest
{
    public string GuestQuestion { get; init; } = string.Empty;
    public global::StayFlow.Api.DTOs.AIContext.AIContext AIContext { get; init; } = new();
    public IReadOnlyCollection<QuestionContextCategory> QuestionCategories { get; init; } = [];
}
