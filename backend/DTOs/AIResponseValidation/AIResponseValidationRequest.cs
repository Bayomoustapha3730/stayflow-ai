using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIPrompt;

namespace StayFlow.Api.DTOs.AIResponseValidation;

public sealed class AIResponseValidationRequest
{
    public string? ModelResponse { get; init; }
    public global::StayFlow.Api.DTOs.AIContext.AIContext AIContext { get; init; } = new();
    public IReadOnlyCollection<QuestionContextCategory> QuestionCategories { get; init; } = [];
    public AIPromptPackage? PromptPackage { get; init; }
    public AIProtectedIdentifiers ProtectedIdentifiers { get; init; } = new();
}
