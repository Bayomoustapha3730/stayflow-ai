using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIContext;

namespace StayFlow.Api.DTOs.AIProvider;

public sealed class AIProviderRequest
{
    public AIPromptPackage PromptPackage { get; init; } = new();
    public IReadOnlyCollection<AIPromptMessage> RenderedMessages { get; init; } = [];
    public AIResponseConstraints ResponseConstraints { get; init; } = new();
    public IReadOnlyCollection<QuestionContextCategory> QuestionCategories { get; init; } = [];
    public string? CorrelationId { get; init; }
}
