using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CustomerBillingReconciliationServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_SumsOnlyPersistedCalculatedUsdChargesAndHonorsHalfOpenRange()
    {
        var (options, companyId, subscriptionId, planId) = CreateOptions();
        await SeedAsync(options,
            Op(companyId, subscriptionId, planId, At(0), CustomerBillableUsageState.Calculated,
                CustomerBillableUsageReason.Calculated, 3, 2, "USD", price: 0.123456789012345678m),
            Op(companyId, subscriptionId, planId, At(1), CustomerBillableUsageState.Calculated,
                CustomerBillableUsageReason.Calculated, 2, 0, "USD", price: 0m),
            Op(companyId, subscriptionId, planId, At(2), CustomerBillableUsageState.Unknown,
                CustomerBillableUsageReason.MissingPrice, 4, 3),
            Op(companyId, subscriptionId, planId, At(3), CustomerBillableUsageState.NotBillable,
                CustomerBillableUsageReason.WithinIncludedAllowance, 5, 0),
            Op(companyId, subscriptionId, planId, At(4), CustomerBillableUsageState.Calculated,
                CustomerBillableUsageReason.Calculated, 7, 0, "USD", price: 9m));

        await using var context = new ApplicationDbContext(options);
        var service = new CustomerBillingReconciliationService(context);
        var result = await service.GetSummaryAsync(
            new CustomerBillingQuery(At(0), At(4)),
            CancellationToken.None);

        Assert.Equal(4, result.TotalOperations);
        Assert.Equal(2, result.CalculatedOperations);
        Assert.Equal(1, result.UnknownOperations);
        Assert.Equal(1, result.NotBillableOperations);
        Assert.Equal(14, result.TotalConsumedQuantity);
        Assert.Equal(5, result.TotalBillableQuantity);
        Assert.Equal(2, result.CalculatedBillableQuantity);
        Assert.Equal(0.123456789012345678m, result.CalculatedChargeAmount);
        Assert.Equal(1, result.CalculatedZeroChargeOperations);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(1, Assert.Single(result.UnknownByReason).Operations);
        Assert.Equal(1, Assert.Single(result.NotBillableByReason).Operations);
    }

    [Fact]
    public async Task GetSummaryAsync_AppliesExactIdentityAndStateReasonFilters()
    {
        var (options, companyId, subscriptionId, planId) = CreateOptions();
        var otherCompanyId = Guid.NewGuid();
        var otherSubscriptionId = Guid.NewGuid();
        var otherPlanId = Guid.NewGuid();
        await SeedAsync(options,
            Op(companyId, subscriptionId, planId, At(1), CustomerBillableUsageState.Unknown,
                CustomerBillableUsageReason.MissingPrice, 3, 2),
            Op(companyId, subscriptionId, planId, At(1), CustomerBillableUsageState.NotBillable,
                CustomerBillableUsageReason.IncludedOnly, 3, 0),
            Op(otherCompanyId, otherSubscriptionId, otherPlanId, At(1), CustomerBillableUsageState.Unknown,
                CustomerBillableUsageReason.MissingPrice, 4, 3, metric: UsageMetric.AiRequests));

        await using var context = new ApplicationDbContext(options);
        var service = new CustomerBillingReconciliationService(context);
        var filter = new CustomerBillingQuery(
            At(0),
            At(2),
            companyId,
            subscriptionId,
            planId,
            UsageMetric.AiTokens,
            CustomerBillableUsageState.Unknown,
            CustomerBillableUsageReason.MissingPrice);
        var result = await service.GetSummaryAsync(filter, CancellationToken.None);

        Assert.Equal(1, result.TotalOperations);
        Assert.Equal(1, result.UnknownOperations);
        Assert.Equal(0m, result.CalculatedChargeAmount);
    }

    [Theory]
    [InlineData(CustomerBillingGroupBy.Company)]
    [InlineData(CustomerBillingGroupBy.Subscription)]
    [InlineData(CustomerBillingGroupBy.Plan)]
    [InlineData(CustomerBillingGroupBy.Metric)]
    [InlineData(CustomerBillingGroupBy.BillingPeriod)]
    [InlineData(CustomerBillingGroupBy.BillingState)]
    [InlineData(CustomerBillingGroupBy.BillingReason)]
    [InlineData(CustomerBillingGroupBy.PricingVersion)]
    public async Task GetBreakdownAsync_SupportsBoundedDimensions(CustomerBillingGroupBy groupBy)
    {
        var (options, companyId, subscriptionId, planId) = CreateOptions();
        await SeedAsync(options,
            Op(companyId, subscriptionId, planId, At(0), CustomerBillableUsageState.Calculated,
                CustomerBillableUsageReason.Calculated, 3, 1, "USD", price: 1.25m),
            Op(companyId, subscriptionId, planId, At(1), CustomerBillableUsageState.NotBillable,
                CustomerBillableUsageReason.IncludedOnly, 2, 0));

        await using var context = new ApplicationDbContext(options);
        var service = new CustomerBillingReconciliationService(context);
        var rows = await service.GetBreakdownAsync(
            new CustomerBillingQuery(At(0), At(2)),
            groupBy,
            CancellationToken.None);

        Assert.NotEmpty(rows);
        Assert.Equal(2, rows.Sum(row => row.TotalOperations));
        Assert.Equal(1, rows.Sum(row => row.CalculatedOperations));
        Assert.Equal(1, rows.Sum(row => row.NotBillableOperations));
        Assert.Equal(5, rows.Sum(row => row.ConsumedQuantity));
        Assert.Equal(1, rows.Sum(row => row.BillableQuantity));
        Assert.Equal(1.25m, rows.Sum(row => row.CalculatedChargeAmount));
        Assert.All(rows, row => Assert.Equal("USD", row.Currency));
    }

    [Fact]
    public async Task GetBreakdownAsync_KeepsDifferentPeriodsDistinctWithinSameMonth()
    {
        var (options, companyId, subscriptionId, planId) = CreateOptions();
        var firstPeriod = At(0);
        var secondPeriod = At(1);
        await SeedAsync(options,
            Op(companyId, subscriptionId, planId, firstPeriod, CustomerBillableUsageState.NotBillable,
                CustomerBillableUsageReason.WithinIncludedAllowance, 1, 0),
            Op(companyId, subscriptionId, planId, secondPeriod, CustomerBillableUsageState.NotBillable,
                CustomerBillableUsageReason.WithinIncludedAllowance, 1, 0));

        await using var context = new ApplicationDbContext(options);
        var rows = await new CustomerBillingReconciliationService(context).GetBreakdownAsync(
            new CustomerBillingQuery(At(0), At(2)),
            CustomerBillingGroupBy.BillingPeriod,
            CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal([firstPeriod, secondPeriod], rows.Select(row => row.PeriodStartUtc!.Value));
    }

    [Fact]
    public async Task GetReconciliationAsync_DetectsPersistedAccountingMismatches()
    {
        var (options, companyId, subscriptionId, planId) = CreateOptions();
        var subscription = new TenantSubscription
        {
            Id = subscriptionId,
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            CurrentPeriodStartUtc = At(0),
            CurrentPeriodEndUtc = At(10)
        };
        var healthy = Op(companyId, subscriptionId, planId, At(1),
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated, 2, 2, "USD", price: 1m);
        var mismatch = Op(companyId, subscriptionId, planId, At(2),
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated, 2, 3, "EUR", price: -1m, usageAfter: 3);
        mismatch.UsageOperation.Metric = UsageMetric.ApiRequests.ToStorageValue();
        mismatch.UsageOperation.PeriodStartUtc = At(3);

        await using (var context = new ApplicationDbContext(options))
        {
            context.UsageOperations.Add(healthy.UsageOperation);
            context.UsageOperations.Add(mismatch.UsageOperation);
            context.TenantSubscriptions.Add(subscription);
            context.CustomerBillableUsageOperations.Add(healthy);
            context.CustomerBillableUsageOperations.Add(mismatch);
            await context.SaveChangesAsync();
        }

        await using var verify = new ApplicationDbContext(options);
        var result = await new CustomerBillingReconciliationService(verify).GetReconciliationAsync(
            new CustomerBillingQuery(At(0), At(5)),
            CancellationToken.None);

        Assert.Equal(2, result.TotalOperationsExamined);
        Assert.Equal(1, result.HealthyOperations);
        Assert.Equal(1, result.AnomalousOperations);
        Assert.Equal(0, result.AnomalyCounts.CompanyMismatch);
        Assert.Equal(1, result.AnomalyCounts.MetricMismatch);
        Assert.Equal(1, result.AnomalyCounts.PeriodMismatch);
        Assert.Equal(1, result.AnomalyCounts.InvalidCalculatedMonetaryFacts);
        Assert.Equal(1, result.AnomalyCounts.QuantityMismatch);
        Assert.Equal(1, result.AnomalyCounts.BillableQuantityExceedsConsumed);
        Assert.Equal(0, result.AnomalyCounts.SubscriptionCompanyMismatch);
        Assert.Equal(0, result.AnomalyCounts.SubscriptionPlanMismatch);
    }

    [Fact]
    public async Task GetReconciliationAsync_DetectsNotBillableAndUnknownMonetaryFacts()
    {
        var (options, companyId, subscriptionId, planId) = CreateOptions();
        await SeedAsync(options,
            Op(companyId, subscriptionId, planId, At(1), CustomerBillableUsageState.NotBillable,
                CustomerBillableUsageReason.WithinIncludedAllowance, 1, 0, "USD", price: 0m),
            Op(companyId, subscriptionId, planId, At(1), CustomerBillableUsageState.Unknown,
                CustomerBillableUsageReason.MissingPrice, 1, 1, "USD", price: 0m),
            Op(companyId, subscriptionId, planId, At(1), CustomerBillableUsageState.Unknown,
                CustomerBillableUsageReason.MissingPrice, 1, 0, "USD"));

        await using var context = new ApplicationDbContext(options);
        var result = await new CustomerBillingReconciliationService(context).GetReconciliationAsync(
            new CustomerBillingQuery(At(0), At(2)),
            CancellationToken.None);

        Assert.Equal(3, result.AnomalousOperations);
        Assert.Equal(1, result.AnomalyCounts.UnexpectedNotBillableMonetaryFacts);
        Assert.Equal(2, result.AnomalyCounts.UnexpectedUnknownMonetaryFacts);
    }

    [Fact]
    public void ValidateQuery_RequiresUtcIncreasingRangeAtMost366DaysAndKnownEnums()
    {
        Assert.Throws<ArgumentException>(() =>
            CustomerBillingReconciliationService.ValidateQuery(new CustomerBillingQuery(
                At(0).ToOffset(TimeSpan.FromHours(1)), At(1))));
        Assert.Throws<ArgumentException>(() =>
            CustomerBillingReconciliationService.ValidateQuery(new CustomerBillingQuery(At(1), At(1))));
        Assert.Throws<ArgumentException>(() =>
            CustomerBillingReconciliationService.ValidateQuery(new CustomerBillingQuery(At(0), At(367))));
        Assert.Throws<ArgumentException>(() =>
            CustomerBillingReconciliationService.ValidateQuery(new CustomerBillingQuery(
                At(0), At(1), Metric: (UsageMetric)999)));
    }

    private static CustomerBillableUsageOperation Op(
        Guid companyId,
        Guid subscriptionId,
        Guid planId,
        DateTimeOffset periodStart,
        CustomerBillableUsageState state,
        CustomerBillableUsageReason reason,
        long consumed,
        long billable,
        string? currency = null,
        decimal? price = null,
        UsageMetric metric = UsageMetric.AiTokens,
        long? usageAfter = null)
    {
        var usageOperation = new UsageOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Metric = metric.ToStorageValue(),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            PeriodStartUtc = periodStart,
            Quantity = consumed
        };
        return CustomerBillableUsageOperation.Create(
            companyId,
            usageOperation,
            subscriptionId,
            planId,
            metric,
            periodStart.AddHours(1),
            periodStart.AddDays(28),
            consumed,
            0,
            usageAfter ?? consumed,
            100,
            billable,
            state,
            reason,
            state == CustomerBillableUsageState.Calculated ? Guid.NewGuid() : null,
            state == CustomerBillableUsageState.Calculated ? 1m : null,
            price,
            price,
            currency);
    }

    private static async Task SeedAsync(
        DbContextOptions<ApplicationDbContext> options,
        params CustomerBillableUsageOperation[] operations)
    {
        await using var context = new ApplicationDbContext(options);
        context.CustomerBillableUsageOperations.AddRange(operations);
        await context.SaveChangesAsync();
    }

    private static (DbContextOptions<ApplicationDbContext> Options, Guid CompanyId, Guid SubscriptionId, Guid PlanId)
        CreateOptions()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"customer-billing-reconciliation-{Guid.NewGuid():N}")
            .Options;
        return (options, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }

    private static DateTimeOffset At(int days) =>
        new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days);
}
