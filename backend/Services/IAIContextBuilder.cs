using StayFlow.Api.DTOs.AIContext;

namespace StayFlow.Api.Services;

public interface IAIContextBuilder
{
    Task<AIContextBuildResult> BuildAsync(AIContextRequest request, CancellationToken cancellationToken);
}
