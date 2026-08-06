using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class NoOpSubscriptionEntitlementService : ISubscriptionEntitlementService
{
    public static NoOpSubscriptionEntitlementService Instance { get; } = new();

    private NoOpSubscriptionEntitlementService()
    {
    }

    public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var periodStart = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1).AddTicks(-1);
        return Task.FromResult(new SubscriptionSnapshot(
            companyId,
            Guid.Empty,
            Guid.Empty,
            "NoOp",
            "NoOp",
            SubscriptionStatus.Active.ToStorageValue(),
            true,
            periodStart,
            periodEnd,
            [],
            []));
    }

    public Task EnsureFeatureEnabledAsync(Guid companyId, string featureKey, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<UsageConsumptionResult> ConsumeQuotaAsync(Guid companyId, UsageMetric metric, long quantity, string idempotencyKey, CancellationToken cancellationToken)
    {
        return Task.FromResult(new UsageConsumptionResult(metric, null, 0, quantity, true, false));
    }

    public async Task<SubscriptionSnapshot> UpdatePlanAsync(Guid companyId, Guid? planId, string? planName, string? notes, CancellationToken cancellationToken)
    {
        return await GetCurrentSnapshotAsync(companyId, cancellationToken);
    }
}