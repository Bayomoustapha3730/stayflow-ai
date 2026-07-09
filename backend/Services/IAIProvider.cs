using StayFlow.Api.DTOs.AIProvider;

namespace StayFlow.Api.Services;

public interface IAIProvider
{
    Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken);
}
