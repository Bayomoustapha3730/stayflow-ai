using StayFlow.Api.DTOs.AIOrchestration;

namespace StayFlow.Api.Services;

public interface IAIOrchestrator
{
    Task<AIOrchestrationResult> ProcessAsync(AIOrchestrationRequest request, CancellationToken cancellationToken);
}
