using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class CustomerUsagePricingResolver(
    DbContextOptions<ApplicationDbContext> dbContextOptions) : ICustomerUsagePricingResolver
{
    public async Task<CustomerUsagePricingResolution> ResolveAsync(
        Guid subscriptionPlanId,
        UsageMetric metric,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new ApplicationDbContext(dbContextOptions);
        return await ResolveWithinContextAsync(
            dbContext,
            subscriptionPlanId,
            metric,
            occurredAtUtc,
            cancellationToken);
    }

    internal static async Task<CustomerUsagePricingResolution> ResolveWithinContextAsync(
        ApplicationDbContext dbContext,
        Guid subscriptionPlanId,
        UsageMetric metric,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var matchingVersions = await dbContext.CustomerUsagePriceVersions
            .AsNoTracking()
            .Where(version => version.SubscriptionPlanId == subscriptionPlanId
                && version.Metric == metric
                && version.EffectiveFromUtc <= occurredAtUtc
                && (version.EffectiveToUtc == null || occurredAtUtc < version.EffectiveToUtc))
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matchingVersions.Count > 1)
        {
            return Unknown(CustomerUsagePricingResolutionReason.AmbiguousPrice);
        }

        if (matchingVersions.Count == 0)
        {
            return Unknown(CustomerUsagePricingResolutionReason.MissingPrice);
        }

        var match = matchingVersions[0];
        return match.BillingMode switch
        {
            CustomerUsageBillingMode.PerUnitOverage => new CustomerUsagePricingResolution(
                CustomerUsagePricingResolutionState.Resolved,
                CustomerUsagePricingResolutionReason.Resolved,
                match.Id,
                match.SubscriptionPlanId,
                match.Metric,
                match.EffectiveFromUtc,
                match.EffectiveToUtc,
                match.UnitQuantity,
                match.UnitPrice,
                match.Currency),
            CustomerUsageBillingMode.IncludedOnly => NotBillable(
                CustomerUsagePricingResolutionReason.IncludedOnly,
                match),
            CustomerUsageBillingMode.NotBillable => NotBillable(
                CustomerUsagePricingResolutionReason.MetricNotBillable,
                match),
            _ => Unknown(CustomerUsagePricingResolutionReason.UnsupportedBillingMode)
        };
    }

    private static CustomerUsagePricingResolution NotBillable(
        CustomerUsagePricingResolutionReason reason,
        CustomerUsagePriceVersion version) =>
        new(
            CustomerUsagePricingResolutionState.NotBillable,
            reason,
            version.Id,
            version.SubscriptionPlanId,
            version.Metric,
            version.EffectiveFromUtc,
            version.EffectiveToUtc);

    private static CustomerUsagePricingResolution Unknown(CustomerUsagePricingResolutionReason reason) =>
        new(CustomerUsagePricingResolutionState.Unknown, reason);
}
