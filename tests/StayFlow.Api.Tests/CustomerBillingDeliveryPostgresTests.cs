using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CustomerBillingDeliveryPostgresTests : IAsyncLifetime
{
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
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var priceVersionId = Guid.NewGuid();
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
            IdempotencyKey = $"delivery-{Guid.NewGuid():N}",
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
}
