using StayFlow.Api.Services;

namespace StayFlow.Api.Models;

public enum CustomerUsageBillingMode
{
    Unsupported = 0,
    IncludedOnly = 1,
    PerUnitOverage = 2,
    NotBillable = 3
}

public sealed class CustomerUsagePriceVersion : AuditableEntity
{
    private CustomerUsagePriceVersion()
    {
    }

    public Guid SubscriptionPlanId { get; private set; }
    public UsageMetric Metric { get; private set; }
    public CustomerUsageBillingMode BillingMode { get; private set; }
    public decimal UnitQuantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset EffectiveFromUtc { get; private set; }
    public DateTimeOffset? EffectiveToUtc { get; private set; }

    public SubscriptionPlan SubscriptionPlan { get; private set; } = null!;

    internal static CustomerUsagePriceVersion Create(CustomerUsagePriceVersionDraft draft)
    {
        return new CustomerUsagePriceVersion
        {
            SubscriptionPlanId = draft.SubscriptionPlanId,
            Metric = draft.Metric,
            BillingMode = draft.BillingMode,
            UnitQuantity = draft.UnitQuantity,
            UnitPrice = draft.UnitPrice,
            Currency = draft.Currency,
            EffectiveFromUtc = draft.EffectiveFromUtc,
            EffectiveToUtc = draft.EffectiveToUtc
        };
    }
}
