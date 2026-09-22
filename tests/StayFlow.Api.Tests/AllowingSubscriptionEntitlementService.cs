using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class AllowingSubscriptionEntitlementService : ISubscriptionEntitlementService
{
    public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new SubscriptionSnapshot(
            CompanyId: companyId,
            SubscriptionId: Guid.Empty,
            PlanId: Guid.Empty,
            PlanName: "Allowing",
            PlanDisplayName: "Allowing",
            SubscriptionStatus: SubscriptionStatus.Active.ToStorageValue(),
            IsEnterprise: true,
            CurrentPeriodStartUtc: DateTimeOffset.UtcNow.AddDays(-1),
            CurrentPeriodEndUtc: DateTimeOffset.UtcNow.AddDays(30),
            Features: [],
            Quotas: []));
    }

    public Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return Task.FromResult<SubscriptionSnapshot?>(new SubscriptionSnapshot(
            CompanyId: companyId,
            SubscriptionId: Guid.Empty,
            PlanId: Guid.Empty,
            PlanName: "Allowing",
            PlanDisplayName: "Allowing",
            SubscriptionStatus: SubscriptionStatus.Active.ToStorageValue(),
            IsEnterprise: true,
            CurrentPeriodStartUtc: DateTimeOffset.UtcNow.AddDays(-1),
            CurrentPeriodEndUtc: DateTimeOffset.UtcNow.AddDays(30),
            Features: [],
            Quotas: []));
    }

    public Task EnsureFeatureEnabledAsync(Guid companyId, string featureKey, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<UsageConsumptionResult> ConsumeQuotaAsync(
        Guid companyId,
        UsageMetric metric,
        long quantity,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new UsageConsumptionResult(
            Metric: metric,
            Limit: null,
            PreviousUsage: 0,
            UpdatedUsage: quantity,
            IsUnlimited: true,
            WasIdempotentReplay: false));
    }

    public Task<SubscriptionSnapshot> UpdatePlanAsync(
        Guid companyId,
        Guid? planId,
        string? planName,
        string? notes,
        CancellationToken cancellationToken)
    {
        return GetCurrentSnapshotAsync(companyId, cancellationToken);
    }
}
