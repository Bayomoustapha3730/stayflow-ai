using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class NoOpSubscriptionEntitlementServiceTests
{
    [Fact]
    public async Task TryGetCurrentSnapshotAsync_ReturnsNull()
    {
        var service = NoOpSubscriptionEntitlementService.Instance;

        var snapshot = await service.TryGetCurrentSnapshotAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_ReturnsSyntheticUnlimitedSnapshot()
    {
        var service = NoOpSubscriptionEntitlementService.Instance;
        var companyId = Guid.NewGuid();

        var snapshot = await service.GetCurrentSnapshotAsync(companyId, CancellationToken.None);

        Assert.Equal(companyId, snapshot.CompanyId);
        Assert.Equal(Guid.Empty, snapshot.SubscriptionId);
        Assert.Equal(Guid.Empty, snapshot.PlanId);
        Assert.Equal(SubscriptionStatus.Active.ToStorageValue(), snapshot.SubscriptionStatus);
        Assert.True(snapshot.IsEnterprise);
        Assert.Empty(snapshot.Features);
        Assert.Empty(snapshot.Quotas);
    }
}
