using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class ConciergeLanguageModelProviderFactory(
    IServiceProvider serviceProvider,
    IOptions<GroundedConciergeOptions> options) : IConciergeLanguageModelProviderFactory
{
    private readonly GroundedConciergeOptions options = options.Value;

    public IConciergeLanguageModel GetProvider()
    {
        if (!this.options.Enabled)
        {
            return serviceProvider.GetRequiredService<DevelopmentConciergeLanguageModel>();
        }

        return serviceProvider.GetRequiredService<DevelopmentConciergeLanguageModel>();
    }
}
