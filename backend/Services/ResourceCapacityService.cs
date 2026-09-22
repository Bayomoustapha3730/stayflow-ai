using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class ResourceCapacityService(
    ApplicationDbContext dbContext,
    ISubscriptionEntitlementService subscriptionEntitlementService) : IResourceCapacityService
{
    public async Task EnsureCapacityAsync(Guid companyId, UsageMetric metric, CancellationToken cancellationToken)
    {
        var entitlement = await GetEntitlementAsync(companyId, metric, cancellationToken);
        if (entitlement.IsUnlimited || entitlement.Limit is null)
        {
            return;
        }

        var currentCount = await GetCurrentCountAsync(companyId, metric, cancellationToken);
        if (currentCount >= entitlement.Limit.Value)
        {
            throw new QuotaExceededException(metric.ToStorageValue(), entitlement.Limit, 1, currentCount);
        }
    }

    public Task<long> GetCurrentCountAsync(Guid companyId, UsageMetric metric, CancellationToken cancellationToken)
    {
        return metric switch
        {
            UsageMetric.Users => dbContext.OrganizationMembers.LongCountAsync(member =>
                member.CompanyId == companyId
                && member.Status == OrganizationMemberStatus.Active.ToStorageValue(), cancellationToken),
            UsageMetric.Properties => dbContext.Properties.LongCountAsync(property =>
                property.CompanyId == companyId && !property.IsDeleted, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Resource capacity is only defined for Users and Properties.")
        };
    }

    private async Task<QuotaSnapshot> GetEntitlementAsync(Guid companyId, UsageMetric metric, CancellationToken cancellationToken)
    {
        var snapshot = await subscriptionEntitlementService.GetCurrentSnapshotAsync(companyId, cancellationToken);
        var entitlement = snapshot.Quotas.FirstOrDefault(quota => quota.Metric == metric);
        if (entitlement is null)
        {
            throw new ForbiddenOperationException(
                $"Quota metric '{metric}' is not enabled for the current subscription.",
                "quota_metric_not_enabled");
        }

        return entitlement;
    }
}
