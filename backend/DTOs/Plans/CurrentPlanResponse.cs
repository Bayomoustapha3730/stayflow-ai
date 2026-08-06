namespace StayFlow.Api.DTOs.Plans;

public sealed class CurrentPlanResponse
{
    public Guid CompanyId { get; init; }
    public Guid SubscriptionId { get; init; }
    public Guid PlanId { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public string PlanDisplayName { get; init; } = string.Empty;
    public string SubscriptionStatus { get; init; } = string.Empty;
    public bool IsEnterprise { get; init; }
    public DateTimeOffset CurrentPeriodStartUtc { get; init; }
    public DateTimeOffset CurrentPeriodEndUtc { get; init; }
    public IReadOnlyCollection<FeatureEntitlementDto> Features { get; init; } = [];
    public IReadOnlyCollection<QuotaUsageDto> Quotas { get; init; } = [];
}

public sealed class FeatureEntitlementDto
{
    public string Key { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
}

public sealed class QuotaUsageDto
{
    public string Metric { get; init; } = string.Empty;
    public string EntitlementKey { get; init; } = string.Empty;
    public long? Limit { get; init; }
    public long Used { get; init; }
    public long? Remaining { get; init; }
    public bool IsUnlimited { get; init; }
    public string Unit { get; init; } = "count";
    public DateTimeOffset PeriodStartUtc { get; init; }
    public DateTimeOffset PeriodEndUtc { get; init; }
}