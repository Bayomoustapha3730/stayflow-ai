using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Services.AI.Validation;

public interface IAIReplyOutputValidator
{
    AIReplyValidationResult Validate(
        AIReplyOperation operation,
        string? output,
        IReadOnlyCollection<string> suggestions,
        int maxOutputCharacters,
        int expectedSuggestionCount,
        bool contextIncomplete);
}
