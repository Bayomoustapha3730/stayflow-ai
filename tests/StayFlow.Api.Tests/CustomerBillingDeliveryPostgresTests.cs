using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Tests;

public sealed class CustomerBillingDeliveryPostgresTests : IAsyncLifetime
{
    private static readonly Guid TestPlanId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid TestPriceVersionId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private readonly string databaseName = $"stayflow_billing_delivery_{Guid.NewGuid():N}";
    private string maintenanceConnectionString = string.Empty;
    private DbContextOptions<ApplicationDbContext> dbOptions = null!;

    public async Task InitializeAsync()
    {
        var testConnection = Environment.GetEnvironmentVariable("STAYFLOW_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(testConnection))
        {
            throw new InvalidOperationException(
                "STAYFLOW_TEST_POSTGRES_CONNECTION must point to a disposable PostgreSQL test cluster.");
        }

        var maintenanceBuilder = new NpgsqlConnectionStringBuilder(testConnection)
        {
            Database = "postgres"
        };
        maintenanceConnectionString = maintenanceBuilder.ConnectionString;
        await using (var maintenanceConnection = new NpgsqlConnection(maintenanceConnectionString))
        {
            await maintenanceConnection.OpenAsync();
            await using var createCommand = new NpgsqlCommand(
                $"CREATE DATABASE \"{databaseName}\"",
                maintenanceConnection);
            await createCommand.ExecuteNonQueryAsync();
        }

        var databaseBuilder = new NpgsqlConnectionStringBuilder(maintenanceConnectionString)
        {
            Database = databaseName
        };
        dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(databaseBuilder.ConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(dbOptions);
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(maintenanceConnectionString))
        {
            return;
        }

        await using var maintenanceConnection = new NpgsqlConnection(maintenanceConnectionString);
        await maintenanceConnection.OpenAsync();
        await using (var terminateCommand = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @databaseName",
            maintenanceConnection))
        {
            terminateCommand.Parameters.AddWithValue("databaseName", databaseName);
            await terminateCommand.ExecuteNonQueryAsync();
        }

        await using var dropCommand = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{databaseName}\"",
            maintenanceConnection);
        await dropCommand.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task ProductionUsageReplay_ConcurrentOutboxRepairCreatesOneDelivery()
    {
        await SeedBillingOperationAsync();
        await using var seed = new ApplicationDbContext(dbOptions);
        var usage = await seed.UsageOperations.SingleAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<UsageConsumptionResult> ReplayAsync()
        {
            await using var context = new ApplicationDbContext(dbOptions);
            var service = new SubscriptionEntitlementService(context,
                new CustomerBillingDeliveryCoordinator(new CustomerBillingDeliveryStore(context)),
                NullLogger<SubscriptionEntitlementService>.Instance);
            await start.Task;
            return await service.ConsumeQuotaAsync(usage.CompanyId, UsageMetric.AiRequests,
                usage.Quantity, usage.IdempotencyKey, CancellationToken.None);
        }

        var first = ReplayAsync();
        var second = ReplayAsync();
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.WasIdempotentReplay));
        await using var verification = new ApplicationDbContext(dbOptions);
        Assert.Single(await verification.CustomerBillingDeliveries.ToListAsync());
        Assert.Single(await verification.CustomerBillableUsageOperations.ToListAsync());
        Assert.Single(await verification.UsageOperations.ToListAsync());
        Assert.Empty(await verification.UsageRecords.ToListAsync());
    }

    [PostgresFact]
    public async Task PrepareAsync_ConcurrentCallsCreateOneCanonicalDelivery()
    {
        var billingOperationId = await SeedBillingOperationAsync();
        var results = await Task.WhenAll(
            PrepareAsync(billingOperationId),
            PrepareAsync(billingOperationId));

        Assert.Equal(results[0].Delivery!.Id, results[1].Delivery!.Id);
        await using var verify = new ApplicationDbContext(dbOptions);
        Assert.Single(await verify.CustomerBillingDeliveries.ToListAsync());
    }

    [PostgresFact]
    public async Task TryStartDispatchAsync_ConcurrentClaimsHaveOneOwnerAndDeliveredCannotBeReclaimed()
    {
        var billingOperationId = await SeedBillingOperationAsync();
        var prepared = await PrepareAsync(billingOperationId);
        var deliveryId = prepared.Delivery!.Id;

        var claims = await Task.WhenAll(
            TryStartAsync(deliveryId, At(11)),
            TryStartAsync(deliveryId, At(12)));
        Assert.Single(claims, claimed => claimed);
        Assert.Single(claims, claimed => !claimed);

        await using (var context = new ApplicationDbContext(dbOptions))
        {
            Assert.True(await new CustomerBillingDeliveryStore(context)
                .RecordDeliveredAsync(deliveryId, "external-object", "provider-request", At(13)));
        }

        Assert.False(await TryStartAsync(deliveryId, At(14)));
    }

    [PostgresFact]
    public async Task TryStartDispatchAsync_IndeterminateOutcomeCannotBeReclaimed()
    {
        var billingOperationId = await SeedBillingOperationAsync();
        var prepared = await PrepareAsync(billingOperationId);
        var deliveryId = prepared.Delivery!.Id;
        await using (var context = new ApplicationDbContext(dbOptions))
        {
            var store = new CustomerBillingDeliveryStore(context);
            Assert.True(await store.TryStartDispatchAsync(deliveryId, At(11)));
            Assert.True(await store.RecordIndeterminateAsync(
                deliveryId,
                null,
                "provider-request",
                "timeout-ambiguous",
                At(12)));
        }

        Assert.False(await TryStartAsync(deliveryId, At(13)));
    }

    [PostgresFact]
    public async Task Dispatcher_ConcurrentCallsClaimAndSendExactlyOnce()
    {
        var billingOperationId = await SeedBillingOperationAsync();
        var prepared = await PrepareAsync(billingOperationId);
        var deliveryId = prepared.Delivery!.Id;
        var transport = new CountingTransport();

        var discoveries = await Task.WhenAll(DiscoverAsync(), DiscoverAsync());
        Assert.All(discoveries, candidates => Assert.Contains(deliveryId, candidates));
        var results = await Task.WhenAll(
            DispatchAsync(deliveryId, transport),
            DispatchAsync(deliveryId, transport));

        Assert.Equal(1, transport.SendCount);
        Assert.Single(results, result => result.Outcome == CustomerBillingDeliveryDispatcherOutcome.Delivered);
        Assert.Single(results, result => result.Outcome == CustomerBillingDeliveryDispatcherOutcome.NotEligible);
        await using var verify = new ApplicationDbContext(dbOptions);
        var delivery = await verify.CustomerBillingDeliveries.SingleAsync(item => item.Id == deliveryId);
        Assert.Equal(CustomerBillingDeliveryState.Delivered, delivery.DeliveryState);
        Assert.Equal(1, delivery.AttemptCount);
    }

    private async Task<IReadOnlyList<Guid>> DiscoverAsync()
    {
        await using var context = new ApplicationDbContext(dbOptions);
        return await new CustomerBillingDeliveryStore(context).GetDispatchCandidatesAsync(
            25,
            At(10),
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
    }

    [PostgresFact]
    public async Task Store_DispatchDiscoveryUsesRetryDelayAndExcludesIndeterminate()
    {
        var billingOperationId = await SeedBillingOperationAsync();
        var prepared = await PrepareAsync(billingOperationId);
        var deliveryId = prepared.Delivery!.Id;

        await using (var context = new ApplicationDbContext(dbOptions))
        {
            var candidates = await new CustomerBillingDeliveryStore(context).GetDispatchCandidatesAsync(
                25,
                At(10),
                TimeSpan.FromMinutes(5),
                CancellationToken.None);
            Assert.Contains(deliveryId, candidates);
        }

        var result = await DispatchAsync(deliveryId, new CountingTransport(
            CustomerBillingExternalTransportOutcome.RetryableUnavailable));
        Assert.Equal(CustomerBillingDeliveryDispatcherOutcome.RetryableFailure, result.Outcome);
        var now = DateTimeOffset.UtcNow.AddSeconds(1);
        await using (var context = new ApplicationDbContext(dbOptions))
        {
            var store = new CustomerBillingDeliveryStore(context);
            Assert.Empty(await store.GetDispatchCandidatesAsync(
                25,
                now,
                TimeSpan.FromHours(1),
                CancellationToken.None));
            Assert.Contains(deliveryId, await store.GetDispatchCandidatesAsync(
                25,
                now,
                TimeSpan.Zero,
                CancellationToken.None));
            Assert.True(await store.TryStartDispatchAsync(deliveryId, now));
            Assert.True(await store.RecordIndeterminateAsync(deliveryId, null, null, "uncertain", now));
            Assert.DoesNotContain(deliveryId, await store.GetDispatchCandidatesAsync(
                25,
                now.AddSeconds(1),
                TimeSpan.Zero,
                CancellationToken.None));
        }
    }

    [PostgresFact]
    public async Task Store_ReconciliationDiscoveryUsesDurableCadence()
    {
        var billingOperationId = await SeedBillingOperationAsync();
        var prepared = await PrepareAsync(billingOperationId);
        var deliveryId = prepared.Delivery!.Id;
        await using var context = new ApplicationDbContext(dbOptions);
        var store = new CustomerBillingDeliveryStore(context);
        Assert.True(await store.TryStartDispatchAsync(deliveryId, At(11)));
        Assert.True(await store.RecordIndeterminateAsync(deliveryId, null, null, "uncertain", At(12)));

        Assert.Empty(await store.GetReconciliationCandidatesAsync(
            25,
            At(12).AddMinutes(4),
            TimeSpan.FromMinutes(5),
            CancellationToken.None));
        Assert.Contains(deliveryId, await store.GetReconciliationCandidatesAsync(
            25,
            At(12).AddMinutes(5),
            TimeSpan.FromMinutes(5),
            CancellationToken.None));
        Assert.True(await store.RecordReconciliationAttemptAsync(deliveryId, At(13), CancellationToken.None));
        Assert.Empty(await store.GetReconciliationCandidatesAsync(
            25,
            At(13).AddMinutes(4),
            TimeSpan.FromMinutes(5),
            CancellationToken.None));
    }

    [PostgresFact]
    public async Task Store_AbandonedDispatchRecoveryIsConditionalAndDoesNotIncrementAttempts()
    {
        var billingOperationId = await SeedBillingOperationAsync();
        var prepared = await PrepareAsync(billingOperationId);
        var deliveryId = prepared.Delivery!.Id;
        await using (var context = new ApplicationDbContext(dbOptions))
        {
            var store = new CustomerBillingDeliveryStore(context);
            Assert.True(await store.TryStartDispatchAsync(deliveryId, At(11)));
            Assert.Empty(await store.GetAbandonedDispatchCandidatesAsync(25, At(10), CancellationToken.None));
            Assert.Contains(deliveryId, await store.GetAbandonedDispatchCandidatesAsync(25, At(11), CancellationToken.None));
            Assert.True(await store.MarkAbandonedDispatchIndeterminateAsync(
                deliveryId,
                At(11),
                At(12),
                CancellationToken.None));
            Assert.False(await store.MarkAbandonedDispatchIndeterminateAsync(
                deliveryId,
                At(11),
                At(13),
                CancellationToken.None));
        }

        await using var verify = new ApplicationDbContext(dbOptions);
        var recovered = await verify.CustomerBillingDeliveries.SingleAsync(item => item.Id == deliveryId);
        Assert.Equal(CustomerBillingDeliveryState.Indeterminate, recovered.DeliveryState);
        Assert.Equal(1, recovered.AttemptCount);
        Assert.Equal("WorkerRecoveryRequired", recovered.LastFailureCode);
        Assert.Equal(At(11), recovered.LastAttemptAtUtc);
        Assert.Equal(At(12), recovered.IndeterminateAtUtc);
    }

    private async Task<CustomerBillingDeliveryDispatcherResult> DispatchAsync(
        Guid deliveryId,
        CountingTransport transport)
    {
        await using var context = new ApplicationDbContext(dbOptions);
        var dispatcher = new CustomerBillingDeliveryDispatcher(
            new CustomerBillingDeliveryStore(context),
            transport,
            Options.Create(new CustomerUsageBillingOptions
            {
                Enabled = true,
                Metrics = new Dictionary<string, CustomerUsageBillingMetricOptions>(StringComparer.Ordinal)
                {
                    [UsageMetric.AiRequests.ToString()] = new()
                    {
                        Transport = "StripeMeter",
                        CustomerPayloadKey = "customer",
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
        return await dispatcher.DispatchAsync(deliveryId, CancellationToken.None);
    }

    private async Task<CustomerBillingDeliveryPreparationResult> PrepareAsync(Guid billingOperationId)
    {
        await using var context = new ApplicationDbContext(dbOptions);
        return await new CustomerBillingDeliveryCoordinator(new CustomerBillingDeliveryStore(context))
            .PrepareAsync(billingOperationId, At(10), CancellationToken.None);
    }

    private async Task<bool> TryStartAsync(Guid deliveryId, DateTimeOffset at)
    {
        await using var context = new ApplicationDbContext(dbOptions);
        return await new CustomerBillingDeliveryStore(context).TryStartDispatchAsync(deliveryId, at);
    }

    private async Task<Guid> SeedBillingOperationAsync()
    {
        var companyId = Guid.NewGuid();
        var planId = TestPlanId;
        var subscriptionId = Guid.NewGuid();
        var priceVersionId = TestPriceVersionId;
        var periodStart = At(0);
        var periodEnd = At(28);
        await using var context = new ApplicationDbContext(dbOptions);

        var company = new Company
        {
            Id = companyId,
            Name = "Billing delivery PostgreSQL test",
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
            IdempotencyKey = $"delivery-{Guid.NewGuid():N}",
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
            2,
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            priceVersionId,
            1m,
            2m,
            6m,
            "USD");

        context.Companies.Add(company);
        context.SubscriptionPlans.Add(plan);
        context.TenantSubscriptions.Add(subscription);
        context.CustomerUsagePriceVersions.Add(priceVersion);
        context.UsageOperations.Add(usageOperation);
        context.CustomerBillableUsageOperations.Add(billingOperation);
        await context.SaveChangesAsync();
        return billingOperation.Id;
    }

    private static DateTimeOffset At(int days) =>
        new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days);

    private sealed class CountingTransport(
        CustomerBillingExternalTransportOutcome outcome = CustomerBillingExternalTransportOutcome.Delivered)
        : ICustomerBillingExternalTransport
    {
        private int sendCount;

        public int SendCount => Volatile.Read(ref sendCount);

        public async Task<CustomerBillingExternalResult> SendAsync(
            CustomerBillingExternalRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref sendCount);
            await Task.Delay(50, cancellationToken);
            return new CustomerBillingExternalResult(
                outcome,
                outcome == CustomerBillingExternalTransportOutcome.Delivered
                    ? "test_delivered"
                    : "test_retryable_unavailable",
                "external-object",
                "provider-request");
        }

        public Task<CustomerBillingExternalReconciliationResult> ReconcileAsync(
            CustomerBillingExternalReconciliationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CustomerBillingExternalReconciliationResult(
                CustomerBillingExternalReconciliationOutcome.Unsupported,
                "test_unsupported"));
    }
}
