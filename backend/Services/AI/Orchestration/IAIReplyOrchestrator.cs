namespace StayFlow.Api.Services.AI.Orchestration;

public interface IAIReplyOrchestrator
{
    Task<AIReplyOrchestrationResult?> OrchestrateAsync(
        Guid companyId,
        AIReplyOrchestrationRequest request,
        CancellationToken cancellationToken);
}
