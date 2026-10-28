using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.Data;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CustomerBillableUsageOperationTests
{
    [Theory]
    [InlineData(50, 10, 100, 0)]
    [InlineData(90, 10, 100, 0)]
    [InlineData(99, 3, 100, 2)]
    [InlineData(105, 3, 100, 3)]
    public void Calculator_OnlyChargesQuantityAboveIncludedAllowance(
        long usageBefore,
        long consumed,
        long allowance,
        long expectedBillable)
    {
        var result = CustomerUsageChargeCalculator.Calculate(usageBefore, consumed, allowance);

        Assert.Equal(checked(usageBefore + consumed), result.UsageAfter);
        Assert.Equal(expectedBillable, result.BillableQuantity);
    }

    [Fact]
    public void Calculator_UsesUnitQuantityDecimalPrecisionAndAllowsZeroPrice()
    {
        var precise = CustomerUsageChargeCalculator.Calculate(
            usageBefore: 99,
            consumedQuantity: 3,
            includedQuantity: 100,
            unitQuantity: 10m,
            unitPrice: 0.123456789012345678m);
        var zero = CustomerUsageChargeCalculator.Calculate(
            usageBefore: 99,
            consumedQuantity: 3,
            includedQuantity: 100,
            unitQuantity: 10m,
            unitPrice: 0m);

        Assert.Equal(2, precise.BillableQuantity);
        Assert.Equal(0.0246913578024691356m, precise.ChargeAmount);
        Assert.Equal(0m, zero.ChargeAmount);
    }

    [Theory]
    [InlineData(CustomerUsagePricingResolutionState.NotBillable, CustomerUsagePricingResolutionReason.IncludedOnly, CustomerBillableUsageState.NotBillable, CustomerBillableUsageReason.IncludedOnly)]
    [InlineData(CustomerUsagePricingResolutionState.NotBillable, CustomerUsagePricingResolutionReason.MetricNotBillable, CustomerBillableUsageState.NotBillable, CustomerBillableUsageReason.MetricNotBillable)]
    [InlineData(CustomerUsagePricingResolutionState.Unknown, CustomerUsagePricingResolutionReason.MissingPrice, CustomerBillableUsageState.Unknown, CustomerBillableUsageReason.MissingPrice)]
    [InlineData(CustomerUsagePricingResolutionState.Unknown, CustomerUsagePricingResolutionReason.AmbiguousPrice, CustomerBillableUsageState.Unknown, CustomerBillableUsageReason.AmbiguousPrice)]
    [InlineData(CustomerUsagePricingResolutionState.Unknown, CustomerUsagePricingResolutionReason.UnsupportedBillingMode, CustomerBillableUsageState.Unknown, CustomerBillableUsageReason.UnsupportedBillingMode)]
    public void Calculator_PreservesBoundedPricingResolutionReasons(
        CustomerUsagePricingResolutionState resolutionState,
        CustomerUsagePricingResolutionReason resolutionReason,
        CustomerBillableUsageState expectedState,
        CustomerBillableUsageReason expectedReason)
    {
        var result = CustomerUsageChargeCalculator.ResolvePrice(
            2,
            new CustomerUsagePricingResolution(resolutionState, resolutionReason));

        Assert.Equal(expectedState, result.State);
        Assert.Equal(expectedReason, result.Reason);
        Assert.Null(result.ChargeAmount);
        Assert.Null(result.Currency);
    }

    [Fact]
    public void Calculator_UnknownResolvedPayloadCannotBecomeAZeroCharge()
    {
        var result = CustomerUsageChargeCalculator.ResolvePrice(
            2,
            new CustomerUsagePricingResolution(
                CustomerUsagePricingResolutionState.Resolved,
                CustomerUsagePricingResolutionReason.Resolved,
                UnitQuantity: 1m,
                UnitPrice: 0m,
                Currency: "USD"));

        Assert.Equal(CustomerBillableUsageState.Unknown, result.State);
        Assert.Equal(CustomerBillableUsageReason.UnsupportedBillingMode, result.Reason);
        Assert.Null(result.ChargeAmount);
    }

    [Fact]
    public void Calculator_ResolvedZeroPriceRemainsCalculated()
    {
        var result = CustomerUsageChargeCalculator.ResolvePrice(
            2,
            new CustomerUsagePricingResolution(
                CustomerUsagePricingResolutionState.Resolved,
                CustomerUsagePricingResolutionReason.Resolved,
                PricingVersionId: Guid.NewGuid(),
                UnitQuantity: 1m,
                UnitPrice: 0m,
                Currency: "USD"));

        Assert.Equal(CustomerBillableUsageState.Calculated, result.State);
        Assert.Equal(CustomerBillableUsageReason.Calculated, result.Reason);
        Assert.Equal(0m, result.ChargeAmount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Calculator_RejectsOverflowAndInvalidQuantities()
    {
        Assert.Throws<OverflowException>(() =>
            CustomerUsageChargeCalculator.Calculate(long.MaxValue, 1, long.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CustomerUsageChargeCalculator.Calculate(-1, 1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CustomerUsageChargeCalculator.Calculate(0, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CustomerUsageChargeCalculator.Calculate(0, 1, -1));
    }

    [Fact]
    public async Task ConsumeQuotaAsync_PersistsOneImmutableBillingFactAndReplayReturnsIt()
    {
        var harness = await CreateHarnessAsync(limit: 100, initialUsage: 99);
        var first = await harness.Service.ConsumeQuotaAsync(
            harness.CompanyId, UsageMetric.AiTokens, 1, "same-operation", CancellationToken.None);
        var original = await harness.DbContext.CustomerBillableUsageOperations.SingleAsync();

        harness.Subscription.SubscriptionPlanId = harness.SecondPlanId;
        await harness.DbContext.SaveChangesAsync();
        var replay = await harness.Service.ConsumeQuotaAsync(
            harness.CompanyId, UsageMetric.AiTokens, 1, "same-operation", CancellationToken.None);

        Assert.False(first.WasIdempotentReplay);
        Assert.True(replay.WasIdempotentReplay);
        Assert.Equal(original.Id, (await harness.DbContext.CustomerBillableUsageOperations.SingleAsync()).Id);
        Assert.Equal(harness.PlanId, original.SubscriptionPlanId);
        Assert.Equal(1, await harness.DbContext.UsageOperations.CountAsync());
        Assert.Equal(1, await harness.DbContext.CustomerBillableUsageOperations.CountAsync());
        Assert.Equal(CustomerBillableUsageState.NotBillable, original.BillingState);
        Assert.Equal(CustomerBillableUsageReason.WithinIncludedAllowance, original.BillingReason);
        Assert.Equal(99, original.UsageBefore);
        Assert.Equal(100, original.UsageAfter);
        Assert.Equal(100, original.IncludedQuantity);
        Assert.Equal(0, original.BillableQuantity);
        Assert.Null(original.ChargeAmount);
    }

    [Fact]
    public async Task ConsumeQuotaAsync_RejectsReplayWithConflictingQuantity()
    {
        var harness = await CreateHarnessAsync(limit: 100);
        await harness.Service.ConsumeQuotaAsync(
            harness.CompanyId, UsageMetric.AiTokens, 1, "conflicting-operation", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            harness.Service.ConsumeQuotaAsync(
                harness.CompanyId, UsageMetric.AiTokens, 2, "conflicting-operation", CancellationToken.None));

        Assert.Equal("usage_operation_conflict", exception.ErrorCode);
        Assert.Equal(1, await harness.DbContext.UsageOperations.CountAsync());
        Assert.Equal(1, await harness.DbContext.CustomerBillableUsageOperations.CountAsync());
    }

    [Fact]
    public async Task ConsumeQuotaAsync_PreservesExistingHardQuotaAndDoesNotPersistRejectedUsage()
    {
        var harness = await CreateHarnessAsync(limit: 100, initialUsage: 99);
        await harness.Service.ConsumeQuotaAsync(
            harness.CompanyId, UsageMetric.AiTokens, 1, "reaches-limit", CancellationToken.None);

        await Assert.ThrowsAsync<QuotaExceededException>(() =>
            harness.Service.ConsumeQuotaAsync(
                harness.CompanyId, UsageMetric.AiTokens, 1, "over-limit", CancellationToken.None));

        Assert.Equal(100, (await harness.DbContext.UsageRecords.SingleAsync()).QuantityUsed);
        Assert.Equal(1, await harness.DbContext.UsageOperations.CountAsync());
        Assert.Equal(1, await harness.DbContext.CustomerBillableUsageOperations.CountAsync());
    }

    [Fact]
    public async Task ConsumeQuotaAsync_FailsClosedForLegacyOperationWithoutBillingFact()
    {
        var harness = await CreateHarnessAsync(limit: 100);
        harness.DbContext.UsageOperations.Add(new UsageOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = harness.CompanyId,
            Metric = UsageMetric.AiTokens.ToStorageValue(),
            PeriodStartUtc = harness.PeriodStartUtc,
            IdempotencyKey = "legacy-operation",
            Quantity = 1
        });
        await harness.DbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            harness.Service.ConsumeQuotaAsync(
                harness.CompanyId, UsageMetric.AiTokens, 1, "legacy-operation", CancellationToken.None));

        Assert.Equal("customer_billable_usage_legacy_missing", exception.ErrorCode);
        Assert.Empty(await harness.DbContext.CustomerBillableUsageOperations.ToListAsync());
    }

    [Fact]
    public async Task ApplicationDbContext_RejectsMutationAndDeletionOfBillingFacts()
    {
        var harness = await CreateHarnessAsync(limit: 10);
        await harness.Service.ConsumeQuotaAsync(
            harness.CompanyId, UsageMetric.AiTokens, 1, "immutable-operation", CancellationToken.None);
        var billable = await harness.DbContext.CustomerBillableUsageOperations.SingleAsync();

        harness.DbContext.Entry(billable).Property(item => item.BillableQuantity).CurrentValue = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.DbContext.SaveChangesAsync());

        harness.DbContext.Entry(billable).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task RelationalSchema_EnforcesSingleBillingFactPerUsageOperation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var usageOperation = new UsageOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Metric = UsageMetric.AiTokens.ToStorageValue(),
            IdempotencyKey = "relational-operation",
            PeriodStartUtc = DateTimeOffset.UtcNow.AddDays(-1),
            Quantity = 1
        };
        var periodEnd = DateTimeOffset.UtcNow.AddDays(1);
        context.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Relational billing test",
            Slug = $"relational-billing-{companyId:N}",
            NormalizedSlug = $"RELATIONAL-BILLING-{companyId:N}",
            Status = "Active",
            Email = $"{companyId:N}@billing.test",
            PhoneNumber = "0000000000",
            CountryCode = "US",
            TimeZone = "UTC"
        });
        context.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = $"RelationalBilling-{planId:N}",
            DisplayName = "Relational billing test",
            Description = "Relational billing test plan"
        });
        context.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = subscriptionId,
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = usageOperation.PeriodStartUtc,
            CurrentPeriodEndUtc = periodEnd
        });
        context.UsageOperations.Add(usageOperation);
        await context.SaveChangesAsync();

        context.CustomerBillableUsageOperations.Add(CustomerBillableUsageOperation.Create(
            companyId,
            usageOperation,
            subscriptionId,
            planId,
            UsageMetric.AiTokens,
            DateTimeOffset.UtcNow,
            periodEnd,
            consumedQuantity: 1,
            usageBefore: 0,
            usageAfter: 1,
            includedQuantity: 10,
            billableQuantity: 0,
            CustomerBillableUsageState.NotBillable,
            CustomerBillableUsageReason.WithinIncludedAllowance));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var persistedUsageOperation = await context.UsageOperations.SingleAsync();
        context.CustomerBillableUsageOperations.Add(CustomerBillableUsageOperation.Create(
            companyId,
            persistedUsageOperation,
            subscriptionId,
            planId,
            UsageMetric.AiTokens,
            DateTimeOffset.UtcNow,
            periodEnd,
            consumedQuantity: 1,
            usageBefore: 0,
            usageAfter: 1,
            includedQuantity: 10,
            billableQuantity: 0,
            CustomerBillableUsageState.NotBillable,
            CustomerBillableUsageReason.WithinIncludedAllowance));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public void CalculationContract_HasNoProviderCostOrTokenTelemetryInputs()
    {
        var parameters = typeof(CustomerUsageChargeCalculator)
            .GetMethod(nameof(CustomerUsageChargeCalculator.Calculate))!
            .GetParameters();

        Assert.Equal(
            ["usageBefore", "consumedQuantity", "includedQuantity", "unitQuantity", "unitPrice"],
            parameters.Select(parameter => parameter.Name));
        Assert.DoesNotContain("ProviderCostAmount", string.Join(',', typeof(CustomerBillableUsageOperation)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)));
        Assert.DoesNotContain("TotalTokens", string.Join(',', typeof(CustomerBillableUsageOperation)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)));
    }

    private static async Task<Harness> CreateHarnessAsync(long limit, long initialUsage = 0)
    {
        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var secondPlanId = Guid.NewGuid();
        var periodStartUtc = DateTimeOffset.UtcNow.AddDays(-1);
        var periodEndUtc = DateTimeOffset.UtcNow.AddDays(10);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"customer-billable-tests-{Guid.NewGuid():N}")
            .Options;
        var dbContext = new ApplicationDbContext(options);
        var subscription = new TenantSubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = periodStartUtc,
            CurrentPeriodEndUtc = periodEndUtc
        };

        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Customer billing test",
            Slug = $"customer-billing-{companyId:N}",
            NormalizedSlug = $"CUSTOMER-BILLING-{companyId:N}",
            Status = "Active",
            Email = $"{companyId:N}@billing.test",
            PhoneNumber = "0000000000",
            CountryCode = "US",
            TimeZone = "UTC"
        });
        dbContext.SubscriptionPlans.AddRange(
            CreatePlan(planId, limit),
            CreatePlan(secondPlanId, limit));
        dbContext.TenantSubscriptions.Add(subscription);
        if (initialUsage > 0)
        {
            dbContext.UsageRecords.Add(new UsageRecord
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Metric = UsageMetric.AiTokens.ToStorageValue(),
                PeriodStartUtc = periodStartUtc,
                PeriodEndUtc = periodEndUtc,
                QuantityUsed = initialUsage
            });
        }

        await dbContext.SaveChangesAsync();
        return new Harness(
            new SubscriptionEntitlementService(dbContext, NullLogger<SubscriptionEntitlementService>.Instance),
            dbContext,
            companyId,
            planId,
            secondPlanId,
            subscription,
            periodStartUtc);
    }

    private static SubscriptionPlan CreatePlan(Guid id, long limit) =>
        new()
        {
            Id = id,
            Name = $"CustomerBilling-{id:N}",
            DisplayName = $"CustomerBilling-{id:N}",
            Description = "Customer billing test plan",
            IsActive = true,
            Entitlements =
            [
                new PlanEntitlement
                {
                    Id = Guid.NewGuid(),
                    Key = UsageMetric.AiTokens.ToQuotaEntitlementKey(),
                    IsEnabled = true,
                    QuotaLimit = limit,
                    IsUnlimited = false
                }
            ]
        };

    private sealed record Harness(
        SubscriptionEntitlementService Service,
        ApplicationDbContext DbContext,
        Guid CompanyId,
        Guid PlanId,
        Guid SecondPlanId,
        TenantSubscription Subscription,
        DateTimeOffset PeriodStartUtc);
}
