namespace StayFlow.Api.Services.AI.Orchestration;

public interface IConciergeResponseValidator
{
    ConciergeResponseValidationResult Validate(ConciergeLanguageModelRequest request, ConciergeLanguageModelResult result);
}