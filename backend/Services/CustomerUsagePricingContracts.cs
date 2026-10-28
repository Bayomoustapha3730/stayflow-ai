using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed record CustomerUsagePriceVersionDraft(
    Guid SubscriptionPlanId,
    UsageMetric Metric,
    CustomerUsageBillingMode BillingMode,
    decimal UnitQuantity,
    decimal UnitPrice,
    string Currency,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc);

public enum CustomerUsagePricingResolutionState
{
    Resolved,
    NotBillable,
    Unknown
}

public enum CustomerUsagePricingResolutionReason
{
    Resolved,
    IncludedOnly,
    MetricNotBillable,
    MissingPrice,
    AmbiguousPrice,
    UnsupportedBillingMode
}

public sealed record CustomerUsagePricingResolution(
    CustomerUsagePricingResolutionState State,
    CustomerUsagePricingResolutionReason Reason,
    Guid? PricingVersionId = null,
    Guid? SubscriptionPlanId = null,
    UsageMetric? Metric = null,
    DateTimeOffset? EffectiveFromUtc = null,
    DateTimeOffset? EffectiveToUtc = null,
    decimal? UnitQuantity = null,
    decimal? UnitPrice = null,
    string? Currency = null);

public interface ICustomerUsagePricingCatalog
{
    Task<Guid> PublishAsync(CustomerUsagePriceVersionDraft draft, CancellationToken cancellationToken);
}

public interface ICustomerUsagePricingResolver
{
    Task<CustomerUsagePricingResolution> ResolveAsync(
        Guid subscriptionPlanId,
        UsageMetric metric,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);
}

public sealed class CustomerUsagePricingCatalogConflictException(string message) : InvalidOperationException(message);
