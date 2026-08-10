using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface ISubscriptionEntitlementService
{
    Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken);
    Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken);
    Task EnsureFeatureEnabledAsync(Guid companyId, string featureKey, CancellationToken cancellationToken);
    Task<UsageConsumptionResult> ConsumeQuotaAsync(Guid companyId, UsageMetric metric, long quantity, string idempotencyKey, CancellationToken cancellationToken);
    Task<SubscriptionSnapshot> UpdatePlanAsync(Guid companyId, Guid? planId, string? planName, string? notes, CancellationToken cancellationToken);
}

public sealed record FeatureSnapshot(string Key, bool IsEnabled);

public sealed record QuotaSnapshot(
    UsageMetric Metric,
    string EntitlementKey,
    long? Limit,
    long Used,
    long? Remaining,
    bool IsUnlimited,
    string Unit,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc);

public sealed record SubscriptionSnapshot(
    Guid CompanyId,
    Guid SubscriptionId,
    Guid PlanId,
    string PlanName,
    string PlanDisplayName,
    string SubscriptionStatus,
    bool IsEnterprise,
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    IReadOnlyCollection<FeatureSnapshot> Features,
    IReadOnlyCollection<QuotaSnapshot> Quotas);

public sealed record UsageConsumptionResult(
    UsageMetric Metric,
    long? Limit,
    long PreviousUsage,
    long UpdatedUsage,
    bool IsUnlimited,
    bool WasIdempotentReplay);