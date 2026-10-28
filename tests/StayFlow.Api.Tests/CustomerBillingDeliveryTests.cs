using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CustomerBillingDeliveryTests
{
    [Fact]
    public async Task Coordinator_PreparesEligibleCalculatedChargeWithStableHistoricalSnapshots()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 12.345678901234567890m,
            billableQuantity: 3);

        CustomerBillingDeliveryPreparationResult first;
        CustomerBillingDeliveryPreparationResult replay;
        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var coordinator = new CustomerBillingDeliveryCoordinator(new CustomerBillingDeliveryStore(context));
            first = await coordinator.PrepareAsync(harness.BillingOperationId, At(10), CancellationToken.None);
            replay = await coordinator.PrepareAsync(harness.BillingOperationId, At(11), CancellationToken.None);
        }

        Assert.Equal(CustomerBillingDeliveryPreparationStatus.Prepared, first.Status);
        Assert.Equal(CustomerBillingDeliveryPreparationStatus.Prepared, replay.Status);
        Assert.NotNull(first.Delivery);
        Assert.Equal(first.Delivery.Id, replay.Delivery!.Id);
        Assert.Equal(CustomerBillingDeliveryState.Prepared, first.Delivery.DeliveryState);
        Assert.Equal(CustomerBillingDeliveryProvider.Stripe, first.Delivery.DeliveryProvider);
        Assert.Equal(CustomerBillingDeliveryKind.PendingExternalBilling, first.Delivery.DeliveryKind);
        Assert.Equal(harness.CompanyId, first.Delivery.CompanyId);
        Assert.Equal(harness.SubscriptionId, first.Delivery.TenantSubscriptionId);
        Assert.Equal(harness.PlanId, first.Delivery.SubscriptionPlanId);
        Assert.Equal(UsageMetric.AiTokens, first.Delivery.Metric);
        Assert.Equal(At(0), first.Delivery.PeriodStartUtc);
        Assert.Equal(At(28), first.Delivery.PeriodEndUtc);
        Assert.Equal(CustomerBillableUsageState.Calculated, first.Delivery.BillingStateSnapshot);
        Assert.Equal(CustomerBillableUsageReason.Calculated, first.Delivery.BillingReasonSnapshot);
        Assert.Equal(3, first.Delivery.BillableQuantitySnapshot);
        Assert.Equal(12.345678901234567890m, first.Delivery.ChargeAmountSnapshot);
        Assert.Equal("USD", first.Delivery.CurrencySnapshot);
        Assert.Equal(harness.PriceVersionId, first.Delivery.CustomerUsagePriceVersionId);
        Assert.Equal(
            $"stayflow:customer-billing:{first.Delivery.Id:N}",
            first.Delivery.ExternalIdempotencyKey);
        Assert.Equal(first.Delivery.ExternalIdempotencyKey, replay.Delivery.ExternalIdempotencyKey);
        Assert.Equal(1, await CountDeliveriesAsync(harness.Options));
    }

    [Fact]
    public async Task Coordinator_SuppressesCalculatedZeroChargeWithoutDispatchClaim()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 0m,
            billableQuantity: 2);

        await using var context = new ApplicationDbContext(harness.Options);
        var result = await new CustomerBillingDeliveryCoordinator(new CustomerBillingDeliveryStore(context))
            .PrepareAsync(harness.BillingOperationId, At(10), CancellationToken.None);

        Assert.Equal(CustomerBillingDeliveryPreparationStatus.Suppressed, result.Status);
        Assert.NotNull(result.Delivery);
        Assert.Equal(CustomerBillingDeliveryState.Suppressed, result.Delivery.DeliveryState);
        Assert.Equal(CustomerBillingDeliveryReason.ZeroChargeSuppressed, result.Delivery.DeliveryReason);
        Assert.False(await new CustomerBillingDeliveryStore(context)
            .TryStartDispatchAsync(result.Delivery.Id, At(11)));
    }

    [Theory]
    [InlineData(CustomerBillableUsageState.Unknown, CustomerBillableUsageReason.MissingPrice)]
    [InlineData(CustomerBillableUsageState.NotBillable, CustomerBillableUsageReason.IncludedOnly)]
    public async Task Coordinator_DoesNotPrepareUnknownOrNotBillableFacts(
        CustomerBillableUsageState billingState,
        CustomerBillableUsageReason billingReason)
    {
        await using var harness = await CreateHarnessAsync(billingState, billingReason, chargeAmount: null);
        await using var context = new ApplicationDbContext(harness.Options);
        var result = await new CustomerBillingDeliveryCoordinator(new CustomerBillingDeliveryStore(context))
            .PrepareAsync(harness.BillingOperationId, At(10), CancellationToken.None);

        Assert.Equal(CustomerBillingDeliveryPreparationStatus.NotExternallyBillable, result.Status);
        Assert.Null(result.Delivery);
        Assert.Equal(0, await CountDeliveriesAsync(harness.Options));
    }

    [Fact]
    public async Task Store_ClaimsOnceAndDeliveredRowsCannotBeRedispatched()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        Guid deliveryId;
        await using (var prepareContext = new ApplicationDbContext(harness.Options))
        {
            var result = await new CustomerBillingDeliveryCoordinator(new CustomerBillingDeliveryStore(prepareContext))
                .PrepareAsync(harness.BillingOperationId, At(10), CancellationToken.None);
            deliveryId = result.Delivery!.Id;
        }

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var store = new CustomerBillingDeliveryStore(context);
            Assert.True(await store.TryStartDispatchAsync(deliveryId, At(11)));
            Assert.False(await store.TryStartDispatchAsync(deliveryId, At(12)));
            Assert.True(await store.RecordDeliveredAsync(deliveryId, "object-1", "request-1", At(13)));
            Assert.False(await store.TryStartDispatchAsync(deliveryId, At(14)));
        }

        var delivered = await GetDeliveryAsync(harness.Options, deliveryId);
        Assert.Equal(CustomerBillingDeliveryState.Delivered, delivered.DeliveryState);
        Assert.Equal(1, delivered.AttemptCount);
        Assert.Equal(At(11), delivered.FirstAttemptAtUtc);
        Assert.Equal(At(11), delivered.LastAttemptAtUtc);
        Assert.Equal(At(13), delivered.DeliveredAtUtc);
        Assert.Equal("object-1", delivered.ExternalObjectId);
        Assert.Equal("request-1", delivered.ExternalRequestId);
    }

    [Fact]
    public async Task Store_NeverAutomaticallyReclaimsIndeterminateOrPermanentFailures()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);

        await using var context = new ApplicationDbContext(harness.Options);
        var store = new CustomerBillingDeliveryStore(context);
        Assert.True(await store.TryStartDispatchAsync(deliveryId, At(11)));
        Assert.True(await store.RecordIndeterminateAsync(deliveryId, null, "request-2", "timeout-ambiguous", At(12)));
        Assert.False(await store.TryStartDispatchAsync(deliveryId, At(13)));

        var next = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 6m);
        await using (next)
        {
            var permanentDeliveryId = await PrepareDeliveryAsync(next);
            await using var permanentContext = new ApplicationDbContext(next.Options);
            var permanentStore = new CustomerBillingDeliveryStore(permanentContext);
            Assert.True(await permanentStore.TryStartDispatchAsync(permanentDeliveryId, At(11)));
            Assert.True(await permanentStore.RecordFailedAsync(
                permanentDeliveryId,
                CustomerBillingDeliveryFailureCategory.ProviderRejected,
                "rejected",
                "request-3",
                At(12)));
            Assert.False(await permanentStore.TryStartDispatchAsync(permanentDeliveryId, At(13)));
        }

        var indeterminate = await GetDeliveryAsync(harness.Options, deliveryId);
        Assert.Equal(CustomerBillingDeliveryState.Indeterminate, indeterminate.DeliveryState);
        Assert.Equal(At(12), indeterminate.IndeterminateAtUtc);
        Assert.Equal("request-2", indeterminate.ExternalRequestId);
    }

    [Fact]
    public async Task Store_ReclaimsOnlyExplicitlyRetryableFailedDeliveries()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);

        await using var context = new ApplicationDbContext(harness.Options);
        var store = new CustomerBillingDeliveryStore(context);
        Assert.True(await store.TryStartDispatchAsync(deliveryId, At(11)));
        Assert.True(await store.RecordFailedAsync(
            deliveryId,
            CustomerBillingDeliveryFailureCategory.RetryableProviderUnavailable,
            "retryable",
            null,
            At(12)));
        Assert.True(await store.TryStartDispatchAsync(deliveryId, At(13)));
        Assert.Equal(2, (await store.GetByIdAsync(deliveryId, CancellationToken.None))!.AttemptCount);
    }

    [Theory]
    [InlineData(CustomerBillingDeliveryFailureCategory.ProviderUnavailable)]
    [InlineData(CustomerBillingDeliveryFailureCategory.Timeout)]
    [InlineData(CustomerBillingDeliveryFailureCategory.NetworkFailure)]
    public async Task Store_DoesNotRetryAmbiguousOrUnclassifiedFailures(
        CustomerBillingDeliveryFailureCategory failureCategory)
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);

        await using var context = new ApplicationDbContext(harness.Options);
        var store = new CustomerBillingDeliveryStore(context);
        Assert.True(await store.TryStartDispatchAsync(deliveryId, At(11)));
        Assert.True(await store.RecordFailedAsync(deliveryId, failureCategory, "not-retryable", null, At(12)));
        Assert.False(await store.TryStartDispatchAsync(deliveryId, At(13)));
    }

    [Fact]
    public async Task Store_RejectsMutationAndDeletionOfHistoricalSnapshots()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var delivery = await context.CustomerBillingDeliveries.SingleAsync(item => item.Id == deliveryId);
            context.Entry(delivery).Property(item => item.ChargeAmountSnapshot).CurrentValue = 7m;
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var delivery = await context.CustomerBillingDeliveries.SingleAsync(item => item.Id == deliveryId);
            context.CustomerBillingDeliveries.Remove(delivery);
            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Store_UsesRestrictiveForeignKeysAndUniqueCanonicalAndIdempotencyIndexes()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var firstDeliveryId = await PrepareDeliveryAsync(harness);
        Guid secondBillingOperationId;

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var original = await context.CustomerBillableUsageOperations
                .AsNoTracking()
                .SingleAsync(item => item.Id == harness.BillingOperationId);
            var duplicate = CustomerBillingDelivery.Create(
                original,
                CustomerBillingDeliveryState.Prepared,
                CustomerBillingDeliveryReason.Prepared,
                At(10));
            context.CustomerBillingDeliveries.Add(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var original = await context.CustomerBillableUsageOperations
                .AsNoTracking()
                .SingleAsync(item => item.Id == harness.BillingOperationId);
            var usageOperation = new UsageOperation
            {
                Id = Guid.NewGuid(),
                CompanyId = harness.CompanyId,
                Metric = UsageMetric.AiTokens.ToStorageValue(),
                IdempotencyKey = $"second-delivery-{Guid.NewGuid():N}",
                PeriodStartUtc = original.PeriodStartUtc,
                Quantity = 1
            };
            var second = CustomerBillableUsageOperation.Create(
                harness.CompanyId,
                usageOperation,
                harness.SubscriptionId,
                harness.PlanId,
                UsageMetric.AiTokens,
                At(1),
                At(28),
                1,
                0,
                1,
                10,
                1,
                CustomerBillableUsageState.Calculated,
                CustomerBillableUsageReason.Calculated,
                harness.PriceVersionId,
                1m,
                2m,
                2m,
                "USD");
            context.UsageOperations.Add(usageOperation);
            context.CustomerBillableUsageOperations.Add(second);
            await context.SaveChangesAsync();
            secondBillingOperationId = second.Id;
        }

        var secondDelivery = await PrepareDeliveryAsync(harness with
        {
            BillingOperationId = secondBillingOperationId
        });
        var first = await GetDeliveryAsync(harness.Options, firstDeliveryId);
        await using (var context = new ApplicationDbContext(harness.Options))
        {
            await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"CustomerBillingDeliveries\" SET \"ExternalIdempotencyKey\" = {first.ExternalIdempotencyKey} WHERE \"Id\" = {secondDelivery}"));
            await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"CustomerBillableUsageOperations\" WHERE \"Id\" = {harness.BillingOperationId}"));
        }
    }

    [Fact]
    public async Task DeliverySnapshots_AreUnaffectedByLaterPriceVersions()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var newerVersion = CustomerUsagePriceVersion.Create(new CustomerUsagePriceVersionDraft(
                harness.PlanId,
                UsageMetric.AiTokens,
                CustomerUsageBillingMode.PerUnitOverage,
                1m,
                99m,
                "USD",
                At(29),
                null));
            newerVersion.Id = Guid.NewGuid();
            context.CustomerUsagePriceVersions.Add(newerVersion);
            await context.SaveChangesAsync();
        }

        var delivery = await GetDeliveryAsync(harness.Options, deliveryId);
        Assert.Equal(5m, delivery.ChargeAmountSnapshot);
        Assert.Equal(harness.PriceVersionId, delivery.CustomerUsagePriceVersionId);
    }

    [Fact]
    public async Task Store_RejectsPreparationWithNonUtcTimestampAndFailedOutcomeFromWrongState()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        await using var context = new ApplicationDbContext(harness.Options);
        var store = new CustomerBillingDeliveryStore(context);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.PrepareAsync(harness.BillingOperationId, At(10).ToOffset(TimeSpan.FromHours(1))));

        var delivery = await new CustomerBillingDeliveryCoordinator(store)
            .PrepareAsync(harness.BillingOperationId, At(10), CancellationToken.None);
        Assert.False(await store.RecordDeliveredAsync(delivery.Delivery!.Id, null, null, At(11)));
    }

    private static async Task<Guid> PrepareDeliveryAsync(Harness harness)
    {
        await using var context = new ApplicationDbContext(harness.Options);
        var result = await new CustomerBillingDeliveryCoordinator(new CustomerBillingDeliveryStore(context))
            .PrepareAsync(harness.BillingOperationId, At(10), CancellationToken.None);
        return result.Delivery!.Id;
    }

    private static async Task<CustomerBillingDelivery> GetDeliveryAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid deliveryId)
    {
        await using var context = new ApplicationDbContext(options);
        return await context.CustomerBillingDeliveries.SingleAsync(item => item.Id == deliveryId);
    }

    private static async Task<int> CountDeliveriesAsync(DbContextOptions<ApplicationDbContext> options)
    {
        await using var context = new ApplicationDbContext(options);
        return await context.CustomerBillingDeliveries.CountAsync();
    }

    private static async Task<Harness> CreateHarnessAsync(
        CustomerBillableUsageState billingState,
        CustomerBillableUsageReason billingReason,
        decimal? chargeAmount,
        long billableQuantity = 1)
    {
        var connection = new SqliteConnection($"Data Source=customer-billing-delivery-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var priceVersionId = Guid.NewGuid();
        var billingOperationId = Guid.NewGuid();
        var periodStart = At(0);
        var periodEnd = At(28);

        await using (var context = new ApplicationDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var company = new Company
            {
                Id = companyId,
                Name = "Billing delivery test",
                Slug = $"delivery-{companyId:N}",
                NormalizedSlug = $"DELIVERY-{companyId:N}",
                Status = "Active",
                Email = $"{companyId:N}@billing.test",
                PhoneNumber = "0000000000",
                CountryCode = "US",
                TimeZone = "UTC"
            };
            var plan = new SubscriptionPlan
            {
                Id = planId,
                Name = $"DeliveryTest-{planId:N}",
                DisplayName = "Delivery test",
                Description = "Delivery test plan"
            };
            var subscription = new TenantSubscription
            {
                Id = subscriptionId,
                CompanyId = companyId,
                SubscriptionPlanId = planId,
                Status = SubscriptionStatus.Active.ToStorageValue(),
                CurrentPeriodStartUtc = periodStart,
                CurrentPeriodEndUtc = periodEnd
            };
            var priceVersion = CustomerUsagePriceVersion.Create(new CustomerUsagePriceVersionDraft(
                planId,
                UsageMetric.AiTokens,
                CustomerUsageBillingMode.PerUnitOverage,
                1m,
                2m,
                "USD",
                periodStart.AddDays(-1),
                null));
            priceVersion.Id = priceVersionId;
            var usageOperation = new UsageOperation
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Metric = UsageMetric.AiTokens.ToStorageValue(),
                IdempotencyKey = $"delivery-{billingOperationId:N}",
                PeriodStartUtc = periodStart,
                Quantity = 3
            };
            var billingOperation = CustomerBillableUsageOperation.Create(
                companyId,
                usageOperation,
                subscriptionId,
                planId,
                UsageMetric.AiTokens,
                periodStart.AddHours(1),
                periodEnd,
                3,
                0,
                3,
                10,
                billableQuantity,
                billingState,
                billingReason,
                billingState == CustomerBillableUsageState.Calculated ? priceVersionId : null,
                billingState == CustomerBillableUsageState.Calculated ? 1m : null,
                billingState == CustomerBillableUsageState.Calculated ? 2m : null,
                chargeAmount,
                chargeAmount is null ? null : "USD");
            typeof(CustomerBillableUsageOperation)
                .GetProperty(nameof(CustomerBillableUsageOperation.Id))!
                .SetValue(billingOperation, billingOperationId);

            context.Companies.Add(company);
            context.SubscriptionPlans.Add(plan);
            context.TenantSubscriptions.Add(subscription);
            context.CustomerUsagePriceVersions.Add(priceVersion);
            context.UsageOperations.Add(usageOperation);
            context.CustomerBillableUsageOperations.Add(billingOperation);
            await context.SaveChangesAsync();
        }

        return new Harness(
            connection,
            options,
            companyId,
            planId,
            subscriptionId,
            priceVersionId,
            billingOperationId);
    }

    private static DateTimeOffset At(int days) =>
        new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days);

    private sealed record Harness(
        SqliteConnection Connection,
        DbContextOptions<ApplicationDbContext> Options,
        Guid CompanyId,
        Guid PlanId,
        Guid SubscriptionId,
        Guid PriceVersionId,
        Guid BillingOperationId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}
