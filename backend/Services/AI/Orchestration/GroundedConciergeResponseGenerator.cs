using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class GroundedConciergeResponseGenerator(
    IConciergeLanguageModelProviderFactory languageModelProviderFactory,
    IConciergePromptBuilder promptBuilder,
    IConciergeResponseValidator validator,
    IOptions<GroundedConciergeOptions> options,
    ILogger<GroundedConciergeResponseGenerator>? logger = null) : IGroundedConciergeResponseGenerator
{
    private readonly GroundedConciergeOptions options = options.Value;
    private readonly ILogger<GroundedConciergeResponseGenerator> logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GroundedConciergeResponseGenerator>.Instance;

    public async Task<ConciergeLanguageModelResult> GenerateAsync(ConciergeLanguageModelRequest request, CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return new ConciergeLanguageModelResult(
                string.Empty,
                false,
                "Disabled",
                null,
                null,
                0,
                false,
                false,
                ["Disabled"],
                "Provider disabled",
                "Disabled",
                [],
                null,
                null);
        }

        var prompt = promptBuilder.Build(request);
        var languageModel = languageModelProviderFactory.GetProvider();
        var generated = await languageModel.GenerateAsync(request, cancellationToken);

        this.logger.LogInformation(
            "Grounded concierge generation completed. Success={Success} Provider={Provider} Sources={SourceCount} OutputLength={OutputLength}",
            generated.Success,
            generated.Provider,
            generated.SourceArticleIds.Count,
            generated.Output?.Length ?? 0);

        var validation = validator.Validate(request, generated);
        if (!validation.IsValid)
        {
            return generated with { Success = false, ValidationOutcome = validation.Outcome, WarningCodes = validation.ViolationCodes.ToArray() };
        }

        return generated with { Success = true, ValidationOutcome = validation.Outcome, WarningCodes = [] };
    }
}
