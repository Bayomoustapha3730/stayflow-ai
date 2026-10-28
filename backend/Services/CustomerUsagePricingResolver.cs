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
                CustomerUsagePricingResolutionReason.IncludedOnly),
            CustomerUsageBillingMode.NotBillable => NotBillable(
                CustomerUsagePricingResolutionReason.MetricNotBillable),
            _ => Unknown(CustomerUsagePricingResolutionReason.UnsupportedBillingMode)
        };
    }

    private static CustomerUsagePricingResolution NotBillable(CustomerUsagePricingResolutionReason reason) =>
        new(CustomerUsagePricingResolutionState.NotBillable, reason);

    private static CustomerUsagePricingResolution Unknown(CustomerUsagePricingResolutionReason reason) =>
        new(CustomerUsagePricingResolutionState.Unknown, reason);
}
