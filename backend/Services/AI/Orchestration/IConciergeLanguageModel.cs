namespace StayFlow.Api.Services.AI.Orchestration;

public interface IConciergeLanguageModel
{
    Task<ConciergeLanguageModelResult> GenerateAsync(ConciergeLanguageModelRequest request, CancellationToken cancellationToken);
}