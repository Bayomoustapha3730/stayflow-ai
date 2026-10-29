using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Tests;

public sealed class CustomerBillingDeliveryTests
{
    private static readonly Guid TestPlanId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestPriceVersionId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData(5, CustomerBillingDeliveryState.Prepared)]
    [InlineData(0, CustomerBillingDeliveryState.Suppressed)]
    public async Task ProductionUsageReplay_PreparesHistoricalFactOnceWithoutConsumingAgain(
        int chargeAmount, CustomerBillingDeliveryState expectedState)
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated, CustomerBillableUsageReason.Calculated, chargeAmount, relational: false);
        await using var context = new ApplicationDbContext(harness.Options);
        var operation = await context.UsageOperations.SingleAsync();
        var service = new SubscriptionEntitlementService(context,
            new CustomerBillingDeliveryCoordinator(new CustomerBillingDeliveryStore(context)),
            NullLogger<SubscriptionEntitlementService>.Instance);

        var first = await service.ConsumeQuotaAsync(harness.CompanyId, UsageMetric.AiRequests,
            operation.Quantity, operation.IdempotencyKey, CancellationToken.None);
        var replay = await service.ConsumeQuotaAsync(harness.CompanyId, UsageMetric.AiRequests,
            operation.Quantity, operation.IdempotencyKey, CancellationToken.None);

        Assert.True(first.WasIdempotentReplay);
        Assert.True(replay.WasIdempotentReplay);
        var delivery = await context.CustomerBillingDeliveries.SingleAsync();
        Assert.Equal(expectedState, delivery.DeliveryState);
        Assert.Equal(harness.BillingOperationId, delivery.CustomerBillableUsageOperationId);
        Assert.Single(await context.UsageOperations.ToListAsync());
        Assert.Single(await context.CustomerBillableUsageOperations.ToListAsync());
        Assert.Empty(await context.UsageRecords.ToListAsync());
    }

    [Fact]
    public async Task ProductionAdmission_CoordinatorFailureDoesNotPersistUsageOrBilling()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated, CustomerBillableUsageReason.Calculated, 5m, relational: false);
        await using (var context = new ApplicationDbContext(harness.Options))
        {
            context.PlanEntitlements.Add(new PlanEntitlement
            {
                Id = Guid.NewGuid(),
                SubscriptionPlanId = harness.PlanId,
                Key = UsageMetric.AiRequests.ToQuotaEntitlementKey(),
                IsEnabled = true,
                QuotaLimit = 100
            });
            await context.SaveChangesAsync();
            var service = new SubscriptionEntitlementService(context,
                new FailingCoordinator(), NullLogger<SubscriptionEntitlementService>.Instance);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConsumeQuotaAsync(harness.CompanyId, UsageMetric.AiRequests, 1, "failed-new-usage", CancellationToken.None));

            Assert.Equal("coordinator failure", error.Message);
        }

        await using var verification = new ApplicationDbContext(harness.Options);
        Assert.Empty(await verification.UsageRecords.ToListAsync());
        Assert.Single(await verification.UsageOperations.ToListAsync());
        Assert.Single(await verification.CustomerBillableUsageOperations.ToListAsync());
        Assert.Empty(await verification.CustomerBillingDeliveries.ToListAsync());
    }

    [Fact]
    public async Task Coordinator_StagesDeliveryInOperationTransactionAndRollbackLeavesNoDelivery()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated, CustomerBillableUsageReason.Calculated, 5m);
        await using (var context = new ApplicationDbContext(harness.Options))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var operation = await context.CustomerBillableUsageOperations.SingleAsync();
            var result = await new CustomerBillingDeliveryCoordinator(new CustomerBillingDeliveryStore(context))
                .PrepareAsync(operation, At(10), CancellationToken.None);

            Assert.Equal(CustomerBillingDeliveryPreparationStatus.Prepared, result.Status);
            Assert.Empty(await context.CustomerBillingDeliveries.AsNoTracking().ToListAsync());
            await context.SaveChangesAsync();
            Assert.Single(await context.CustomerBillingDeliveries.AsNoTracking().ToListAsync());
            await transaction.RollbackAsync();
        }

        Assert.Equal(0, await CountDeliveriesAsync(harness.Options));
    }

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
        Assert.Equal(UsageMetric.AiRequests, first.Delivery.Metric);
        Assert.Equal(At(0), first.Delivery.PeriodStartUtc);
        Assert.Equal(At(28), first.Delivery.PeriodEndUtc);
        Assert.Equal(At(0).AddHours(1), first.Delivery.OccurredAtUtc);
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

    [Fact]
    public async Task Store_StagingSameOperationTwiceBeforeSaveReusesOneDelivery()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        await using var context = new ApplicationDbContext(harness.Options);
        var operation = await context.CustomerBillableUsageOperations.SingleAsync();
        var store = new CustomerBillingDeliveryStore(context);

        var first = await store.StageAsync(operation, At(10), CancellationToken.None);
        var replay = await store.StageAsync(operation, At(11), CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.Equal(first.Id, replay.Id);
        await context.SaveChangesAsync();
        Assert.Equal(1, await CountDeliveriesAsync(harness.Options));
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
            context.Entry(delivery).Property(item => item.OccurredAtUtc).CurrentValue = At(2);
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
                Metric = UsageMetric.AiRequests.ToStorageValue(),
                IdempotencyKey = $"second-delivery-{Guid.NewGuid():N}",
                PeriodStartUtc = original.PeriodStartUtc,
                Quantity = 1
            };
            var second = CustomerBillableUsageOperation.Create(
                harness.CompanyId,
                usageOperation,
                harness.SubscriptionId,
                harness.PlanId,
                UsageMetric.AiRequests,
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
                UsageMetric.AiRequests,
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

    [Fact]
    public async Task Dispatcher_DeliveredOutcomeClaimsOnceAndPersistsSnapshotAndExternalIds()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m,
            billableQuantity: 3);
        var deliveryId = await PrepareDeliveryAsync(harness);
        var delivery = await GetDeliveryAsync(harness.Options, deliveryId);
        var transport = new StubTransport
        {
            SendResult = new CustomerBillingExternalResult(
                CustomerBillingExternalTransportOutcome.Delivered,
                "meter_event_created",
                "meter-event-1",
                "request-1")
        };

        CustomerBillingDeliveryDispatcherResult result;
        await using (var context = new ApplicationDbContext(harness.Options))
        {
            result = await CreateDispatcher(new CustomerBillingDeliveryStore(context), transport)
                .DispatchAsync(deliveryId, CancellationToken.None);
        }

        var persisted = await GetDeliveryAsync(harness.Options, deliveryId);
        Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.Delivered, result.Outcome);
        Assert.Equal(CustomerBillingDeliveryState.Delivered, persisted.DeliveryState);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal("meter-event-1", persisted.ExternalObjectId);
        Assert.Equal("request-1", persisted.ExternalRequestId);
        Assert.Equal(1, transport.SendCount);
        Assert.Equal(delivery.ExternalIdempotencyKey, transport.LastRequest!.ExternalIdempotencyKey);
        Assert.Equal(delivery.BillableQuantitySnapshot, transport.LastRequest.BillableQuantity);
        Assert.Equal(delivery.ChargeAmountSnapshot, transport.LastRequest.ChargeAmount);
        Assert.Equal(delivery.PeriodStartUtc, transport.LastRequest.PeriodStartUtc);
        Assert.Equal(At(0).AddHours(1), transport.LastRequest.OccurredAtUtc);
    }

    [Fact]
    public async Task Dispatcher_LostClaimAndSuppressedDeliveryNeverSend()
    {
        await using var claimHarness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var claimedId = await PrepareDeliveryAsync(claimHarness);
        var transport = new StubTransport();
        await using (var context = new ApplicationDbContext(claimHarness.Options))
        {
            Assert.True(await new CustomerBillingDeliveryStore(context).TryStartDispatchAsync(claimedId, At(11)));
        }

        await using (var context = new ApplicationDbContext(claimHarness.Options))
        {
            var result = await CreateDispatcher(new CustomerBillingDeliveryStore(context), transport)
                .DispatchAsync(claimedId, CancellationToken.None);
            Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.NotEligible, result.Outcome);
        }

        await using var suppressedHarness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 0m);
        var suppressedId = await PrepareDeliveryAsync(suppressedHarness);
        await using (var context = new ApplicationDbContext(suppressedHarness.Options))
        {
            var result = await CreateDispatcher(new CustomerBillingDeliveryStore(context), transport)
                .DispatchAsync(suppressedId, CancellationToken.None);
            Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.NotEligible, result.Outcome);
        }

        Assert.Equal(0, transport.SendCount);
    }

    [Theory]
    [InlineData(CustomerBillingExternalTransportOutcome.Rejected, CustomerBillingDeliveryFailureCategory.ProviderRejected, CustomerBillingDeliveryDispatcherOutcome.Rejected, false)]
    [InlineData(CustomerBillingExternalTransportOutcome.RetryableUnavailable, CustomerBillingDeliveryFailureCategory.RetryableProviderUnavailable, CustomerBillingDeliveryDispatcherOutcome.RetryableFailure, true)]
    public async Task Dispatcher_PersistsDefinitiveAndRetryableFailuresWithoutImmediateRetry(
        CustomerBillingExternalTransportOutcome transportOutcome,
        CustomerBillingDeliveryFailureCategory failureCategory,
        CustomerBillingDeliveryDispatcherOutcome dispatcherOutcome,
        bool retryAllowed)
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);
        var transport = new StubTransport
        {
            SendResult = new CustomerBillingExternalResult(transportOutcome, "bounded_provider_code", null, "request-1")
        };

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var result = await CreateDispatcher(new CustomerBillingDeliveryStore(context), transport)
                .DispatchAsync(deliveryId, CancellationToken.None);
            Assert.Equal(dispatcherOutcome, result.Outcome);
        }

        var persisted = await GetDeliveryAsync(harness.Options, deliveryId);
        Assert.Equal(CustomerBillingDeliveryState.Failed, persisted.DeliveryState);
        Assert.Equal(failureCategory, persisted.LastFailureCategory);
        Assert.Equal(1, transport.SendCount);

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            Assert.Equal(
                retryAllowed,
                await new CustomerBillingDeliveryStore(context).TryStartDispatchAsync(deliveryId, At(12)));
        }

        Assert.Equal(1, transport.SendCount);
    }

    [Fact]
    public async Task Dispatcher_IndeterminateOutcomeCannotRedispatchAndReconciliationNeverSends()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);
        var transport = new StubTransport
        {
            SendResult = new CustomerBillingExternalResult(
                CustomerBillingExternalTransportOutcome.Indeterminate,
                "stripe_network_outcome_ambiguous")
        };

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var dispatcher = CreateDispatcher(new CustomerBillingDeliveryStore(context), transport);
            var first = await dispatcher.DispatchAsync(deliveryId, CancellationToken.None);
            var second = await dispatcher.DispatchAsync(deliveryId, CancellationToken.None);
            Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.Indeterminate, first.Outcome);
            Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.NotEligible, second.Outcome);
        }

        var persisted = await GetDeliveryAsync(harness.Options, deliveryId);
        Assert.Equal(CustomerBillingDeliveryState.Indeterminate, persisted.DeliveryState);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal(1, transport.SendCount);

        transport.ReconcileResult = new CustomerBillingExternalReconciliationResult(
            CustomerBillingExternalReconciliationOutcome.NotFound,
            "provider_event_not_found");
        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var result = await CreateDispatcher(new CustomerBillingDeliveryStore(context), transport)
                .ReconcileAsync(deliveryId, CancellationToken.None);
            Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.NotFound, result.Outcome);
            Assert.Equal(CustomerBillingDeliveryState.Indeterminate, result.State);
        }

        Assert.Equal(1, transport.SendCount);
        Assert.Equal(1, transport.ReconcileCount);
        Assert.Equal(persisted.ExternalIdempotencyKey, transport.LastReconciliationRequest!.ExternalIdempotencyKey);
    }

    [Theory]
    [InlineData(CustomerBillingExternalReconciliationOutcome.FoundDelivered, CustomerBillingDeliveryState.Delivered, CustomerBillingDeliveryDispatcherOutcome.Delivered)]
    [InlineData(CustomerBillingExternalReconciliationOutcome.FoundRejected, CustomerBillingDeliveryState.Failed, CustomerBillingDeliveryDispatcherOutcome.Rejected)]
    [InlineData(CustomerBillingExternalReconciliationOutcome.Unsupported, CustomerBillingDeliveryState.Indeterminate, CustomerBillingDeliveryDispatcherOutcome.Unsupported)]
    [InlineData(CustomerBillingExternalReconciliationOutcome.StillIndeterminate, CustomerBillingDeliveryState.Indeterminate, CustomerBillingDeliveryDispatcherOutcome.StillIndeterminate)]
    public async Task Dispatcher_ReconciliationPersistsOnlyDefinitiveOutcomes(
        CustomerBillingExternalReconciliationOutcome reconciliationOutcome,
        CustomerBillingDeliveryState expectedState,
        CustomerBillingDeliveryDispatcherOutcome expectedOutcome)
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);
        await MakeIndeterminateAsync(harness, deliveryId);
        var transport = new StubTransport
        {
            ReconcileResult = new CustomerBillingExternalReconciliationResult(
                reconciliationOutcome,
                "reconciled_provider_code",
                "meter-event-2",
                "request-2")
        };

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var result = await CreateDispatcher(new CustomerBillingDeliveryStore(context), transport)
                .ReconcileAsync(deliveryId, CancellationToken.None);
            Assert.Equal(expectedOutcome, result.Outcome);
        }

        var persisted = await GetDeliveryAsync(harness.Options, deliveryId);
        Assert.Equal(expectedState, persisted.DeliveryState);
        Assert.Equal(0, transport.SendCount);
        Assert.Equal(1, transport.ReconcileCount);
        if (reconciliationOutcome == CustomerBillingExternalReconciliationOutcome.FoundDelivered)
        {
            Assert.Equal("meter-event-2", persisted.ExternalObjectId);
            Assert.Equal("request-2", persisted.ExternalRequestId);
        }

        if (reconciliationOutcome == CustomerBillingExternalReconciliationOutcome.FoundRejected)
        {
            Assert.Equal(CustomerBillingDeliveryFailureCategory.ProviderRejected, persisted.LastFailureCategory);
            await using var verify = new ApplicationDbContext(harness.Options);
            Assert.False(await new CustomerBillingDeliveryStore(verify).TryStartDispatchAsync(deliveryId, At(14)));
        }
    }

    [Fact]
    public async Task Dispatcher_UnexpectedTransportExceptionRecordsIndeterminate()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);
        var transport = new StubTransport
        {
            SendException = new HttpRequestException("network outcome unclear")
        };

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var result = await CreateDispatcher(new CustomerBillingDeliveryStore(context), transport)
                .DispatchAsync(deliveryId, CancellationToken.None);
            Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.Indeterminate, result.Outcome);
        }

        var persisted = await GetDeliveryAsync(harness.Options, deliveryId);
        Assert.Equal(CustomerBillingDeliveryState.Indeterminate, persisted.DeliveryState);
        Assert.Equal("transport_exception", persisted.LastFailureCode);
        Assert.Equal(1, transport.SendCount);
    }

    [Fact]
    public async Task Dispatcher_PersistenceFailureDoesNotSendAgainAndMovesToIndeterminate()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);
        var original = await GetDeliveryAsync(harness.Options, deliveryId);
        var transport = new StubTransport
        {
            SendResult = new CustomerBillingExternalResult(
                CustomerBillingExternalTransportOutcome.Delivered,
                "created",
                "external-1")
        };

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var throwingStore = new DeliveredWriteFailingStore(new CustomerBillingDeliveryStore(context));
            var result = await CreateDispatcher(throwingStore, transport).DispatchAsync(deliveryId, CancellationToken.None);
            Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.PersistenceFailure, result.Outcome);
        }

        var persisted = await GetDeliveryAsync(harness.Options, deliveryId);
        Assert.Equal(CustomerBillingDeliveryState.Indeterminate, persisted.DeliveryState);
        Assert.Equal(original.ExternalIdempotencyKey, persisted.ExternalIdempotencyKey);
        Assert.Equal(1, transport.SendCount);
    }

    [Fact]
    public async Task Dispatcher_PersistsDeliveredOutcomeAfterCallerCancellation()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);
        using var cancellation = new CancellationTokenSource();
        var transport = new StubTransport
        {
            SendHandler = (_, _) =>
            {
                cancellation.Cancel();
                return Task.FromResult(new CustomerBillingExternalResult(
                    CustomerBillingExternalTransportOutcome.Delivered,
                    "created",
                    "external-2"));
            }
        };

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var result = await CreateDispatcher(new CustomerBillingDeliveryStore(context), transport)
                .DispatchAsync(deliveryId, cancellation.Token);
            Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.Delivered, result.Outcome);
        }

        Assert.Equal(CustomerBillingDeliveryState.Delivered, (await GetDeliveryAsync(harness.Options, deliveryId)).DeliveryState);
    }

    [Fact]
    public async Task Dispatcher_DisabledUsageBillingDoesNotClaimOrSend()
    {
        await using var harness = await CreateHarnessAsync(
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            chargeAmount: 5m);
        var deliveryId = await PrepareDeliveryAsync(harness);
        var transport = new StubTransport();

        await using (var context = new ApplicationDbContext(harness.Options))
        {
            var result = await CreateDispatcher(
                    new CustomerBillingDeliveryStore(context),
                    transport,
                    enabled: false)
                .DispatchAsync(deliveryId, CancellationToken.None);
            Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.ConfigurationError, result.Outcome);
        }

        var persisted = await GetDeliveryAsync(harness.Options, deliveryId);
        Assert.Equal(CustomerBillingDeliveryState.Prepared, persisted.DeliveryState);
        Assert.Equal(0, persisted.AttemptCount);
        Assert.Equal(0, transport.SendCount);
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

    private static async Task MakeIndeterminateAsync(Harness harness, Guid deliveryId)
    {
        await using var context = new ApplicationDbContext(harness.Options);
        var store = new CustomerBillingDeliveryStore(context);
        Assert.True(await store.TryStartDispatchAsync(deliveryId, At(11)));
        Assert.True(await store.RecordIndeterminateAsync(deliveryId, null, "request-1", "ambiguous", At(12)));
    }

    private static CustomerBillingDeliveryDispatcher CreateDispatcher(
        ICustomerBillingDeliveryStore store,
        StubTransport transport,
        bool enabled = true) =>
        new(
            store,
            transport,
            Options.Create(new CustomerUsageBillingOptions
            {
                Enabled = enabled,
                Metrics = new Dictionary<string, CustomerUsageBillingMetricOptions>(StringComparer.Ordinal)
                {
                    [UsageMetric.AiRequests.ToString()] = new()
                    {
                        Transport = "StripeMeter",
                        CustomerPayloadKey = "stripe_customer_id",
                        ValuePayloadKey = "value",
                        BillableQuantityIsExternalUnit = true,
                        PriceMappings = new Dictionary<string, CustomerUsageBillingPriceMappingOptions>(StringComparer.Ordinal)
                        {
                            [CustomerUsageBillingOptionsValidator.PriceMappingKey(TestPlanId, TestPriceVersionId)] = new()
                            {
                                ResourcesVerified = true,
                                MeterId = "mtr_test123",
                                MeterEventName = "ai_requests",
                                UsagePriceId = "price_test123",
                                Currency = "USD",
                                UnitQuantity = 1m,
                                UnitPrice = 2m,
                                StripeUnitAmountMinor = 200m,
                                RateSemantics = "LinearPerBillableUnit",
                                MappingIdentity = CustomerUsageBillingOptionsValidator.ExpectedMappingIdentity(
                                    TestPlanId, UsageMetric.AiRequests, TestPriceVersionId,
                                    "mtr_test123", "ai_requests", "price_test123")
                            }
                        }
                    }
                }
            }),
            TimeProvider.System,
            NullLogger<CustomerBillingDeliveryDispatcher>.Instance);

    private static async Task<Harness> CreateHarnessAsync(
        CustomerBillableUsageState billingState,
        CustomerBillableUsageReason billingReason,
        decimal? chargeAmount,
        long billableQuantity = 1,
        bool relational = true)
    {
        var connection = new SqliteConnection($"Data Source=customer-billing-delivery-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await connection.OpenAsync();
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var options = relational
            ? builder.UseSqlite(connection).Options
            : builder.UseInMemoryDatabase($"delivery-production-{Guid.NewGuid():N}").Options;
        var companyId = Guid.NewGuid();
        var planId = TestPlanId;
        var subscriptionId = Guid.NewGuid();
        var priceVersionId = TestPriceVersionId;
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
                UsageMetric.AiRequests,
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
                Metric = UsageMetric.AiRequests.ToStorageValue(),
                IdempotencyKey = $"delivery-{billingOperationId:N}",
                PeriodStartUtc = periodStart,
                Quantity = 3
            };
            var billingOperation = CustomerBillableUsageOperation.Create(
                companyId,
                usageOperation,
                subscriptionId,
                planId,
                UsageMetric.AiRequests,
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

    private sealed class StubTransport : ICustomerBillingExternalTransport
    {
        public CustomerBillingExternalResult SendResult { get; set; } =
            new(CustomerBillingExternalTransportOutcome.Indeterminate, "unspecified");

        public CustomerBillingExternalReconciliationResult ReconcileResult { get; set; } =
            new(CustomerBillingExternalReconciliationOutcome.Unsupported, "unsupported");

        public Func<CustomerBillingExternalRequest, CancellationToken, Task<CustomerBillingExternalResult>>? SendHandler { get; set; }
        public Exception? SendException { get; set; }
        public int SendCount { get; private set; }
        public int ReconcileCount { get; private set; }
        public CustomerBillingExternalRequest? LastRequest { get; private set; }
        public CustomerBillingExternalReconciliationRequest? LastReconciliationRequest { get; private set; }

        public Task<CustomerBillingExternalResult> SendAsync(
            CustomerBillingExternalRequest request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            LastRequest = request;
            if (SendException is not null)
            {
                return Task.FromException<CustomerBillingExternalResult>(SendException);
            }

            return SendHandler?.Invoke(request, cancellationToken) ?? Task.FromResult(SendResult);
        }

        public Task<CustomerBillingExternalReconciliationResult> ReconcileAsync(
            CustomerBillingExternalReconciliationRequest request,
            CancellationToken cancellationToken)
        {
            ReconcileCount++;
            LastReconciliationRequest = request;
            return Task.FromResult(ReconcileResult);
        }
    }

    private sealed class FailingCoordinator : ICustomerBillingDeliveryCoordinator
    {
        public Task<CustomerBillingDeliveryPreparationResult> PrepareAsync(
            CustomerBillableUsageOperation operation, DateTimeOffset preparedAtUtc, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("coordinator failure");

        public Task<CustomerBillingDeliveryPreparationResult> PrepareAsync(
            Guid billingOperationId, DateTimeOffset preparedAtUtc, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("coordinator failure");
    }

    private sealed class DeliveredWriteFailingStore(ICustomerBillingDeliveryStore inner) : ICustomerBillingDeliveryStore
    {
        public Task<CustomerBillableUsageOperation?> GetBillingOperationAsync(
            Guid billingOperationId,
            CancellationToken cancellationToken) =>
            inner.GetBillingOperationAsync(billingOperationId, cancellationToken);

        public Task<CustomerBillingDelivery> PrepareAsync(Guid billingOperationId, DateTimeOffset preparedAtUtc) =>
            inner.PrepareAsync(billingOperationId, preparedAtUtc);

        public Task<CustomerBillingDelivery?> StageAsync(
            CustomerBillableUsageOperation operation,
            DateTimeOffset preparedAtUtc,
            CancellationToken cancellationToken) =>
            inner.StageAsync(operation, preparedAtUtc, cancellationToken);

        public Task<CustomerBillingDelivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken) =>
            inner.GetByIdAsync(deliveryId, cancellationToken);

        public Task<CustomerBillingDelivery?> GetByBillingOperationAsync(
            Guid billingOperationId,
            CancellationToken cancellationToken) =>
            inner.GetByBillingOperationAsync(billingOperationId, cancellationToken);

        public Task<IReadOnlyList<Guid>> GetDispatchCandidatesAsync(
            int limit,
            DateTimeOffset now,
            TimeSpan retryDelay,
            CancellationToken cancellationToken) =>
            inner.GetDispatchCandidatesAsync(limit, now, retryDelay, cancellationToken);

        public Task<IReadOnlyList<Guid>> GetReconciliationCandidatesAsync(
            int limit,
            DateTimeOffset now,
            TimeSpan reconciliationDelay,
            CancellationToken cancellationToken) =>
            inner.GetReconciliationCandidatesAsync(limit, now, reconciliationDelay, cancellationToken);

        public Task<IReadOnlyList<Guid>> GetAbandonedDispatchCandidatesAsync(
            int limit,
            DateTimeOffset cutoff,
            CancellationToken cancellationToken) =>
            inner.GetAbandonedDispatchCandidatesAsync(limit, cutoff, cancellationToken);

        public Task<bool> MarkAbandonedDispatchIndeterminateAsync(
            Guid deliveryId,
            DateTimeOffset cutoff,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) =>
            inner.MarkAbandonedDispatchIndeterminateAsync(deliveryId, cutoff, occurredAtUtc, cancellationToken);

        public Task<bool> RecordReconciliationAttemptAsync(
            Guid deliveryId,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) =>
            inner.RecordReconciliationAttemptAsync(deliveryId, occurredAtUtc, cancellationToken);

        public Task<bool> TryStartDispatchAsync(Guid deliveryId, DateTimeOffset occurredAtUtc) =>
            inner.TryStartDispatchAsync(deliveryId, occurredAtUtc);

        public Task<bool> RecordDeliveredAsync(
            Guid deliveryId,
            string? externalObjectId,
            string? externalRequestId,
            DateTimeOffset occurredAtUtc) =>
            throw new InvalidOperationException("Simulated delivery persistence failure.");

        public Task<bool> RecordFailedAsync(
            Guid deliveryId,
            CustomerBillingDeliveryFailureCategory failureCategory,
            string failureCode,
            string? externalRequestId,
            DateTimeOffset occurredAtUtc) =>
            inner.RecordFailedAsync(deliveryId, failureCategory, failureCode, externalRequestId, occurredAtUtc);

        public Task<bool> RecordIndeterminateAsync(
            Guid deliveryId,
            string? externalObjectId,
            string? externalRequestId,
            string? failureCode,
            DateTimeOffset occurredAtUtc) =>
            inner.RecordIndeterminateAsync(deliveryId, externalObjectId, externalRequestId, failureCode, occurredAtUtc);

        public Task<bool> RecordReconciledDeliveredAsync(
            Guid deliveryId,
            string? externalObjectId,
            string? externalRequestId,
            DateTimeOffset occurredAtUtc) =>
            inner.RecordReconciledDeliveredAsync(deliveryId, externalObjectId, externalRequestId, occurredAtUtc);

        public Task<bool> RecordReconciledRejectedAsync(
            Guid deliveryId,
            string failureCode,
            string? externalRequestId,
            DateTimeOffset occurredAtUtc) =>
            inner.RecordReconciledRejectedAsync(deliveryId, failureCode, externalRequestId, occurredAtUtc);
    }

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
