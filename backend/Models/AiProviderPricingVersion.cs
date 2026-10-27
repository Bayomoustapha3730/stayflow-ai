using StayFlow.Api.Services;

namespace StayFlow.Api.Models;

public sealed class AiProviderPricingVersion : AuditableEntity
{
    private AiProviderPricingVersion()
    {
    }

    public string Provider { get; private set; } = string.Empty;
    public string PricingModelKey { get; private set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public decimal InputPrice { get; private set; }
    public decimal OutputPrice { get; private set; }
    public int UnitTokenCount { get; private set; }

    internal static AiProviderPricingVersion Create(AiProviderPricingVersionDraft draft)
    {
        return new AiProviderPricingVersion
        {
            Provider = draft.Provider,
            PricingModelKey = draft.PricingModelKey,
            EffectiveFrom = draft.EffectiveFrom,
            EffectiveTo = draft.EffectiveTo,
            Currency = draft.Currency,
            InputPrice = draft.InputPrice,
            OutputPrice = draft.OutputPrice,
            UnitTokenCount = draft.UnitTokenCount
        };
    }
}
