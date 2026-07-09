using StayFlow.Api.DTOs.AIResponseValidation;

namespace StayFlow.Api.Services;

public interface IAIResponseValidator
{
    AIResponseValidationResult Validate(AIResponseValidationRequest request);
}
