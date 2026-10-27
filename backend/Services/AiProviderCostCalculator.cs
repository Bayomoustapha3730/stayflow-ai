namespace StayFlow.Api.Services;

public sealed class AiProviderCostCalculator(IAiProviderPricingResolver pricingResolver) : IAiProviderCostCalculator
{
    private const decimal MaximumPersistedAmount = 9999999999.999999999999999999m;

    public async Task<AiProviderCostResult> CalculateAsync(
        AiProviderCostFacts facts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.InputTokens < 0 || facts.OutputTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(facts), "Provider token usage cannot be negative.");
        }

        if (facts.ProviderUsageUnavailable)
        {
            if (string.Equals(
                    AiProviderPricingIdentity.NormalizeProvider(facts.Provider ?? string.Empty),
                    "DEVELOPMENT",
                    StringComparison.Ordinal))
            {
                return NotApplicable();
            }

            return Unknown(AiProviderCostReason.ProviderUsageUnavailable);
        }

        var pricing = await pricingResolver.ResolveAsync(
            facts.Provider,
            facts.ResolvedModel,
            facts.DispatchStartedAt,
            cancellationToken);

        if (pricing.State == AiProviderPricingResolutionState.NotApplicable)
        {
            return NotApplicable();
        }

        if (pricing.State == AiProviderPricingResolutionState.Unknown)
        {
            return Unknown(MapReason(pricing.Reason), pricing.PricingVersionId);
        }

        if (facts.InputTokens is null || facts.OutputTokens is null)
        {
            return Unknown(AiProviderCostReason.MissingTokenUsage, pricing.PricingVersionId);
        }

        if (pricing.InputPrice is null
            || pricing.OutputPrice is null
            || pricing.UnitTokenCount is null
            || pricing.UnitTokenCount <= 0
            || !string.Equals(pricing.Currency, "USD", StringComparison.Ordinal))
        {
            return Unknown(AiProviderCostReason.UnsupportedPricingDimension, pricing.PricingVersionId);
        }

        try
        {
            var inputCost = checked((decimal)facts.InputTokens.Value * pricing.InputPrice.Value)
                / pricing.UnitTokenCount.Value;
            var outputCost = checked((decimal)facts.OutputTokens.Value * pricing.OutputPrice.Value)
                / pricing.UnitTokenCount.Value;
            var amount = decimal.Round(checked(inputCost + outputCost), 18, MidpointRounding.ToEven);
            if (amount > MaximumPersistedAmount)
            {
                return Unknown(AiProviderCostReason.CalculationOverflow, pricing.PricingVersionId);
            }

            return new AiProviderCostResult(
                AiProviderCostState.Calculated,
                AiProviderCostReason.Calculated,
                pricing.PricingVersionId,
                amount,
                "USD");
        }
        catch (OverflowException)
        {
            return Unknown(AiProviderCostReason.CalculationOverflow, pricing.PricingVersionId);
        }
    }

    private static AiProviderCostResult NotApplicable() =>
        new(AiProviderCostState.NotApplicable, AiProviderCostReason.NonBillableProvider);

    private static AiProviderCostResult Unknown(AiProviderCostReason reason, Guid? pricingVersionId = null) =>
        new(AiProviderCostState.Unknown, reason, pricingVersionId);

    private static AiProviderCostReason MapReason(AiProviderPricingResolutionReason reason) =>
        reason switch
        {
            AiProviderPricingResolutionReason.MissingResolvedModel => AiProviderCostReason.MissingResolvedModel,
            AiProviderPricingResolutionReason.MissingDispatchTimestamp => AiProviderCostReason.MissingDispatchTimestamp,
            AiProviderPricingResolutionReason.UnknownProvider => AiProviderCostReason.UnknownProvider,
            AiProviderPricingResolutionReason.UnknownModel => AiProviderCostReason.UnknownModel,
            AiProviderPricingResolutionReason.NoEffectivePrice => AiProviderCostReason.NoEffectivePrice,
            AiProviderPricingResolutionReason.AmbiguousPrice => AiProviderCostReason.AmbiguousPrice,
            AiProviderPricingResolutionReason.NonBillableProvider => AiProviderCostReason.NonBillableProvider,
            AiProviderPricingResolutionReason.UnsupportedPricingDimension => AiProviderCostReason.UnsupportedPricingDimension,
            _ => AiProviderCostReason.UnsupportedPricingDimension
        };
}
