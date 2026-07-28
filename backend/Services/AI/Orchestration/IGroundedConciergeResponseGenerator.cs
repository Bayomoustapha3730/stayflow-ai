namespace StayFlow.Api.Services.AI.Orchestration;

public interface IGroundedConciergeResponseGenerator
{
    Task<ConciergeLanguageModelResult> GenerateAsync(ConciergeLanguageModelRequest request, CancellationToken cancellationToken);
}