using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ResourceCapacityServiceTests
{
    [Fact]
    public async Task GetCurrentCountAsync_CountsOnlyActiveUsersAndNonDeletedProperties()
    {
        var companyId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        dbContext.OrganizationMembers.AddRange(
            new OrganizationMember { Id = Guid.NewGuid(), CompanyId = companyId, UserId = Guid.NewGuid(), Status = OrganizationMemberStatus.Active.ToStorageValue() },
            new OrganizationMember { Id = Guid.NewGuid(), CompanyId = companyId, UserId = Guid.NewGuid(), Status = OrganizationMemberStatus.Suspended.ToStorageValue() },
            new OrganizationMember { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = OrganizationMemberStatus.Active.ToStorageValue() });
        dbContext.Properties.AddRange(
            new Property { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Active", IsDeleted = false },
            new Property { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Deleted", IsDeleted = true },
            new Property { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Name = "Other", IsDeleted = false });
        await dbContext.SaveChangesAsync();

        var service = new ResourceCapacityService(dbContext, new FixedEntitlementService());

        Assert.Equal(1, await service.GetCurrentCountAsync(companyId, UsageMetric.Users, CancellationToken.None));
        Assert.Equal(1, await service.GetCurrentCountAsync(companyId, UsageMetric.Properties, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureCapacityAsync_RejectsFiniteLimitAtCapacity()
    {
        var companyId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        dbContext.Properties.Add(new Property { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Existing", IsDeleted = false });
        await dbContext.SaveChangesAsync();

        var service = new ResourceCapacityService(
            dbContext,
            new FixedEntitlementService(CreateQuota(UsageMetric.Properties, 1, false)));

        var exception = await Assert.ThrowsAsync<QuotaExceededException>(() =>
            service.EnsureCapacityAsync(companyId, UsageMetric.Properties, CancellationToken.None));

        Assert.Equal(UsageMetric.Properties.ToStorageValue(), exception.Metric);
        Assert.Equal(1, exception.Current);
        Assert.Equal(1, exception.Limit);
    }

    [Fact]
    public async Task EnsureCapacityAsync_RejectsUsersWhenActiveCountReachesFiniteLimit()
    {
        var companyId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = Guid.NewGuid(),
            Status = OrganizationMemberStatus.Active.ToStorageValue()
        });
        await dbContext.SaveChangesAsync();

        var service = new ResourceCapacityService(
            dbContext,
            new FixedEntitlementService(CreateQuota(UsageMetric.Users, 1, false)));

        await Assert.ThrowsAsync<QuotaExceededException>(() =>
            service.EnsureCapacityAsync(companyId, UsageMetric.Users, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureCapacityAsync_AllowsAdmissionAfterActiveMembershipIsRemoved()
    {
        var companyId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var membership = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = Guid.NewGuid(),
            Status = OrganizationMemberStatus.Active.ToStorageValue()
        };
        dbContext.OrganizationMembers.Add(membership);
        await dbContext.SaveChangesAsync();
        var service = new ResourceCapacityService(
            dbContext,
            new FixedEntitlementService(CreateQuota(UsageMetric.Users, 1, false)));

        await Assert.ThrowsAsync<QuotaExceededException>(() =>
            service.EnsureCapacityAsync(companyId, UsageMetric.Users, CancellationToken.None));

        membership.Status = OrganizationMemberStatus.Removed.ToStorageValue();
        await dbContext.SaveChangesAsync();

        await service.EnsureCapacityAsync(companyId, UsageMetric.Users, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureCapacityAsync_AllowsUnlimitedEntitlement()
    {
        var service = new ResourceCapacityService(
            CreateDbContext(),
            new FixedEntitlementService(CreateQuota(UsageMetric.Users, null, true)));

        await service.EnsureCapacityAsync(Guid.NewGuid(), UsageMetric.Users, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureCapacityAsync_AllowsUnlimitedPropertiesBeyondFiniteThreshold()
    {
        var companyId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        dbContext.Properties.AddRange(
            new Property { Id = Guid.NewGuid(), CompanyId = companyId, Name = "One" },
            new Property { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Two" });
        await dbContext.SaveChangesAsync();

        var service = new ResourceCapacityService(
            dbContext,
            new FixedEntitlementService(CreateQuota(UsageMetric.Properties, 1, true)));

        await service.EnsureCapacityAsync(companyId, UsageMetric.Properties, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureCapacityAsync_RejectsMetricWithoutEntitlement()
    {
        var service = new ResourceCapacityService(CreateDbContext(), new FixedEntitlementService());

        var exception = await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            service.EnsureCapacityAsync(Guid.NewGuid(), UsageMetric.Users, CancellationToken.None));

        Assert.Equal("quota_metric_not_enabled", exception.ErrorCode);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"resource-capacity-{Guid.NewGuid():N}")
            .Options);
    }

    private static QuotaSnapshot CreateQuota(UsageMetric metric, long? limit, bool isUnlimited)
    {
        var periodStart = DateTimeOffset.UtcNow.AddDays(-1);
        return new QuotaSnapshot(
            metric,
            metric.ToQuotaEntitlementKey(),
            limit,
            0,
            limit,
            isUnlimited,
            "count",
            periodStart,
            periodStart.AddDays(30));
    }

    private sealed class FixedEntitlementService(QuotaSnapshot? quota = null) : ISubscriptionEntitlementService
    {
        public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SubscriptionSnapshot(
                companyId,
                Guid.Empty,
                Guid.Empty,
                "Test",
                "Test",
                SubscriptionStatus.Active.ToStorageValue(),
                false,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(30),
                [],
                quota is null ? [] : [quota]));
        }

        public Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken) =>
            GetCurrentSnapshotAsync(companyId, cancellationToken).ContinueWith(task => (SubscriptionSnapshot?)task.Result, cancellationToken);

        public Task EnsureFeatureEnabledAsync(Guid companyId, string featureKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<UsageConsumptionResult> ConsumeQuotaAsync(Guid companyId, UsageMetric metric, long quantity, string idempotencyKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SubscriptionSnapshot> UpdatePlanAsync(Guid companyId, Guid? planId, string? planName, string? notes, CancellationToken cancellationToken) =>
            GetCurrentSnapshotAsync(companyId, cancellationToken);
    }

}