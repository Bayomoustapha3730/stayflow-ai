using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CustomerUsagePricingTests
{
    [Fact]
    public async Task ResolveAsync_UsesExactPlanAndMetricAndHalfOpenEffectiveInterval()
    {
        var (options, catalog, resolver) = CreateServices();
        var planId = Guid.NewGuid();
        var otherPlanId = Guid.NewGuid();
        var firstId = await catalog.PublishAsync(
            Draft(planId, UsageMetric.AiTokens, EffectiveFromUtc: At(10), EffectiveToUtc: At(20), UnitPrice: 1m),
            CancellationToken.None);
        var secondId = await catalog.PublishAsync(
            Draft(planId, UsageMetric.AiTokens, EffectiveFromUtc: At(20), EffectiveToUtc: At(30), UnitPrice: 2m),
            CancellationToken.None);
        var otherPlanIdVersion = await catalog.PublishAsync(
            Draft(otherPlanId, UsageMetric.AiTokens, EffectiveFromUtc: At(10), UnitPrice: 3m),
            CancellationToken.None);

        var before = await resolver.ResolveAsync(planId, UsageMetric.AiTokens, At(9), CancellationToken.None);
        var atStart = await resolver.ResolveAsync(planId, UsageMetric.AiTokens, At(10), CancellationToken.None);
        var beforeEnd = await resolver.ResolveAsync(planId, UsageMetric.AiTokens, At(19), CancellationToken.None);
        var atEnd = await resolver.ResolveAsync(planId, UsageMetric.AiTokens, At(20), CancellationToken.None);
        var differentMetric = await resolver.ResolveAsync(planId, UsageMetric.AiRequests, At(15), CancellationToken.None);
        var differentPlan = await resolver.ResolveAsync(otherPlanId, UsageMetric.AiTokens, At(15), CancellationToken.None);

        Assert.Equal(CustomerUsagePricingResolutionReason.MissingPrice, before.Reason);
        Assert.Equal(firstId, atStart.PricingVersionId);
        Assert.Equal(firstId, beforeEnd.PricingVersionId);
        Assert.Equal(secondId, atEnd.PricingVersionId);
        Assert.Equal(CustomerUsagePricingResolutionReason.MissingPrice, differentMetric.Reason);
        Assert.Equal(otherPlanIdVersion, differentPlan.PricingVersionId);
        Assert.Equal(3m, differentPlan.UnitPrice);
    }

    [Fact]
    public async Task ResolveAsync_ReportsMissingAndResolvesHistoricalVersions()
    {
        var (_, catalog, resolver) = CreateServices();
        var planId = Guid.NewGuid();
        await catalog.PublishAsync(
            Draft(planId, UsageMetric.ApiRequests, EffectiveFromUtc: At(0), EffectiveToUtc: At(10), UnitPrice: 1m),
            CancellationToken.None);
        await catalog.PublishAsync(
            Draft(planId, UsageMetric.ApiRequests, EffectiveFromUtc: At(10), UnitPrice: 2m),
            CancellationToken.None);

        var missing = await resolver.ResolveAsync(planId, UsageMetric.ApiRequests, At(-1), CancellationToken.None);
        var historical = await resolver.ResolveAsync(planId, UsageMetric.ApiRequests, At(5), CancellationToken.None);
        var current = await resolver.ResolveAsync(planId, UsageMetric.ApiRequests, At(15), CancellationToken.None);

        Assert.Equal(CustomerUsagePricingResolutionState.Unknown, missing.State);
        Assert.Equal(CustomerUsagePricingResolutionReason.MissingPrice, missing.Reason);
        Assert.Equal(1m, historical.UnitPrice);
        Assert.Equal(2m, current.UnitPrice);
    }

    [Theory]
    [InlineData(CustomerUsageBillingMode.IncludedOnly, CustomerUsagePricingResolutionState.NotBillable, CustomerUsagePricingResolutionReason.IncludedOnly)]
    [InlineData(CustomerUsageBillingMode.NotBillable, CustomerUsagePricingResolutionState.NotBillable, CustomerUsagePricingResolutionReason.MetricNotBillable)]
    [InlineData(CustomerUsageBillingMode.PerUnitOverage, CustomerUsagePricingResolutionState.Resolved, CustomerUsagePricingResolutionReason.Resolved)]
    public async Task ResolveAsync_ReturnsTheExpectedBillingMode(
        CustomerUsageBillingMode billingMode,
        CustomerUsagePricingResolutionState expectedState,
        CustomerUsagePricingResolutionReason expectedReason)
    {
        var (_, catalog, resolver) = CreateServices();
        var planId = Guid.NewGuid();
        await catalog.PublishAsync(
            Draft(planId, UsageMetric.WhatsAppMessages, BillingMode: billingMode),
            CancellationToken.None);

        var resolution = await resolver.ResolveAsync(planId, UsageMetric.WhatsAppMessages, At(1), CancellationToken.None);

        Assert.Equal(expectedState, resolution.State);
        Assert.Equal(expectedReason, resolution.Reason);
    }

    [Fact]
    public async Task ResolveAsync_KeepsZeroPriceResolved()
    {
        var (_, catalog, resolver) = CreateServices();
        var planId = Guid.NewGuid();
        await catalog.PublishAsync(
            Draft(planId, UsageMetric.Reservations, UnitPrice: 0m),
            CancellationToken.None);

        var result = await resolver.ResolveAsync(planId, UsageMetric.Reservations, At(1), CancellationToken.None);

        Assert.Equal(CustomerUsagePricingResolutionState.Resolved, result.State);
        Assert.Equal(CustomerUsagePricingResolutionReason.Resolved, result.Reason);
        Assert.Equal(0m, result.UnitPrice);
    }

    [Fact]
    public async Task Catalog_RejectsOverlapsButAllowsAdjacentIntervals()
    {
        var (_, catalog, _) = CreateServices();
        var planId = Guid.NewGuid();
        await catalog.PublishAsync(
            Draft(planId, UsageMetric.FileUploads, EffectiveFromUtc: At(0), EffectiveToUtc: At(10)),
            CancellationToken.None);

        await Assert.ThrowsAsync<CustomerUsagePricingCatalogConflictException>(() => catalog.PublishAsync(
            Draft(planId, UsageMetric.FileUploads, EffectiveFromUtc: At(9), EffectiveToUtc: At(20)),
            CancellationToken.None));

        await catalog.PublishAsync(
            Draft(planId, UsageMetric.FileUploads, EffectiveFromUtc: At(10), EffectiveToUtc: At(20)),
            CancellationToken.None);
    }

    [Fact]
    public async Task ResolveAsync_FailsClosedWhenMultipleEffectiveVersionsExist()
    {
        var (options, catalog, resolver) = CreateServices();
        var planId = Guid.NewGuid();
        await catalog.PublishAsync(
            Draft(planId, UsageMetric.StorageBytes, EffectiveFromUtc: At(0), EffectiveToUtc: At(10)),
            CancellationToken.None);

        await using (var context = new ApplicationDbContext(options))
        {
            context.CustomerUsagePriceVersions.Add(CreateVersion(
                Draft(planId, UsageMetric.StorageBytes, EffectiveFromUtc: At(5), EffectiveToUtc: At(15))));
            await context.SaveChangesAsync();
        }

        var result = await resolver.ResolveAsync(planId, UsageMetric.StorageBytes, At(7), CancellationToken.None);

        Assert.Equal(CustomerUsagePricingResolutionState.Unknown, result.State);
        Assert.Equal(CustomerUsagePricingResolutionReason.AmbiguousPrice, result.Reason);
        Assert.Null(result.PricingVersionId);
    }

    [Fact]
    public async Task ResolveAsync_UsesExactUsageMetricIdentity()
    {
        var (_, catalog, resolver) = CreateServices();
        var planId = Guid.NewGuid();
        await catalog.PublishAsync(
            Draft(planId, UsageMetric.AiRequests),
            CancellationToken.None);

        var exact = await resolver.ResolveAsync(planId, UsageMetric.AiRequests, At(1), CancellationToken.None);
        var similarMetric = await resolver.ResolveAsync(planId, UsageMetric.AiTokens, At(1), CancellationToken.None);

        Assert.Equal(CustomerUsagePricingResolutionState.Resolved, exact.State);
        Assert.Equal(CustomerUsagePricingResolutionReason.MissingPrice, similarMetric.Reason);
    }

    [Fact]
    public async Task ResolveAsync_UnknownStoredBillingModeFailsClosed()
    {
        var (options, _, resolver) = CreateServices();
        var planId = Guid.NewGuid();
        await using (var context = new ApplicationDbContext(options))
        {
            context.CustomerUsagePriceVersions.Add(CreateVersion(
                Draft(planId, UsageMetric.Users),
                (CustomerUsageBillingMode)999));
            await context.SaveChangesAsync();
        }

        var result = await resolver.ResolveAsync(planId, UsageMetric.Users, At(1), CancellationToken.None);

        Assert.Equal(CustomerUsagePricingResolutionState.Unknown, result.State);
        Assert.Equal(CustomerUsagePricingResolutionReason.UnsupportedBillingMode, result.Reason);
    }

    [Fact]
    public async Task ApplicationDbContext_RejectsMutationAndDeletionOfPublishedCustomerPrices()
    {
        var (options, catalog, _) = CreateServices();
        var id = await catalog.PublishAsync(Draft(Guid.NewGuid(), UsageMetric.Users), CancellationToken.None);

        await using var context = new ApplicationDbContext(options);
        var version = await context.CustomerUsagePriceVersions.SingleAsync(item => item.Id == id);
        context.Entry(version).Property(item => item.UnitPrice).CurrentValue = 2m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

        context.Entry(version).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Catalog_RejectsInvalidPriceQuantityCurrencyAndEffectiveInterval()
    {
        var (_, catalog, _) = CreateServices();
        var planId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => catalog.PublishAsync(
            Draft(planId, UsageMetric.Users, UnitPrice: -0.01m), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => catalog.PublishAsync(
            Draft(planId, UsageMetric.Users, UnitQuantity: 0m), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => catalog.PublishAsync(
            Draft(planId, UsageMetric.Users, UnitQuantity: -1m), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.PublishAsync(
            Draft(planId, UsageMetric.Users, Currency: "EUR"), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.PublishAsync(
            Draft(planId, UsageMetric.Users, EffectiveFromUtc: At(10), EffectiveToUtc: At(10)), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.PublishAsync(
            Draft(planId, UsageMetric.Users, BillingMode: CustomerUsageBillingMode.Unsupported), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.PublishAsync(
            Draft(Guid.Empty, UsageMetric.Users), CancellationToken.None));
    }

    private static CustomerUsagePriceVersion CreateVersion(
        CustomerUsagePriceVersionDraft draft,
        CustomerUsageBillingMode billingMode = CustomerUsageBillingMode.PerUnitOverage)
    {
        var version = (CustomerUsagePriceVersion)Activator.CreateInstance(
            typeof(CustomerUsagePriceVersion),
            nonPublic: true)!;
        Set(version, nameof(CustomerUsagePriceVersion.SubscriptionPlanId), draft.SubscriptionPlanId);
        Set(version, nameof(CustomerUsagePriceVersion.Metric), draft.Metric);
        Set(version, nameof(CustomerUsagePriceVersion.BillingMode), billingMode);
        Set(version, nameof(CustomerUsagePriceVersion.UnitQuantity), draft.UnitQuantity);
        Set(version, nameof(CustomerUsagePriceVersion.UnitPrice), draft.UnitPrice);
        Set(version, nameof(CustomerUsagePriceVersion.Currency), draft.Currency);
        Set(version, nameof(CustomerUsagePriceVersion.EffectiveFromUtc), draft.EffectiveFromUtc);
        Set(version, nameof(CustomerUsagePriceVersion.EffectiveToUtc), draft.EffectiveToUtc);
        return version;
    }

    private static void Set(CustomerUsagePriceVersion version, string propertyName, object? value) =>
        typeof(CustomerUsagePriceVersion).GetProperty(propertyName)!.SetValue(version, value);

    private static CustomerUsagePriceVersionDraft Draft(
        Guid planId,
        UsageMetric metric,
        CustomerUsageBillingMode BillingMode = CustomerUsageBillingMode.PerUnitOverage,
        decimal UnitQuantity = 1m,
        decimal UnitPrice = 1.25m,
        string Currency = "USD",
        DateTimeOffset? EffectiveFromUtc = null,
        DateTimeOffset? EffectiveToUtc = null) =>
        new(planId, metric, BillingMode, UnitQuantity, UnitPrice, Currency,
            EffectiveFromUtc ?? At(0), EffectiveToUtc);

    private static DateTimeOffset At(int day) =>
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day);

    private static (DbContextOptions<ApplicationDbContext> Options, CustomerUsagePricingCatalog Catalog, CustomerUsagePricingResolver Resolver)
        CreateServices()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"customer-usage-pricing-{Guid.NewGuid():N}")
            .Options;
        return (
            options,
            new CustomerUsagePricingCatalog(options),
            new CustomerUsagePricingResolver(options));
    }
}
