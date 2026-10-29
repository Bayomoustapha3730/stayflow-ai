using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CustomerBillableUsagePostgresTests : IAsyncLifetime
{
    private readonly string databaseName = $"stayflow_customer_billable_test_{Guid.NewGuid():N}";
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
            await using var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", maintenanceConnection);
            await createCommand.ExecuteNonQueryAsync();
        }

        var testDatabaseBuilder = new NpgsqlConnectionStringBuilder(maintenanceConnectionString)
        {
            Database = databaseName
        };
        dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(testDatabaseBuilder.ConnectionString)
            .Options;

        await using var schemaContext = new ApplicationDbContext(dbOptions);
        await schemaContext.Database.EnsureCreatedAsync();
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

        await using var dropCommand = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", maintenanceConnection);
        await dropCommand.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task ConsumeQuotaAsync_ConcurrentBoundaryOperationsPreserveHardQuotaAndAtomicBilling()
    {
        var seed = await SeedAsync(initialUsage: 99);
        var outcomes = await RunConcurrentlyAsync(
            () => ConsumeAsync(seed.CompanyId, "boundary-a"),
            () => ConsumeAsync(seed.CompanyId, "boundary-b"));

        Assert.Single(outcomes, outcome => outcome.Result is not null);
        Assert.Single(outcomes, outcome => outcome.Exception is QuotaExceededException);
        await using var verificationContext = new ApplicationDbContext(dbOptions);
        Assert.Equal(100, (await verificationContext.UsageRecords.SingleAsync()).QuantityUsed);
        Assert.Single(await verificationContext.UsageOperations.ToListAsync());
        var billable = await verificationContext.CustomerBillableUsageOperations.SingleAsync();
        Assert.Equal(0, billable.BillableQuantity);
        Assert.Equal(CustomerBillableUsageReason.WithinIncludedAllowance, billable.BillingReason);
        Assert.Equal(billable.UsageOperationId,
            (await verificationContext.UsageOperations.SingleAsync()).Id);
    }

    [PostgresFact]
    public async Task ConsumeQuotaAsync_ConcurrentReplayCreatesOneQuotaAndBillingOperation()
    {
        var seed = await SeedAsync(initialUsage: 0);
        var outcomes = await RunConcurrentlyAsync(
            () => ConsumeAsync(seed.CompanyId, "same-idempotency-key"),
            () => ConsumeAsync(seed.CompanyId, "same-idempotency-key"));

        Assert.All(outcomes, outcome => Assert.Null(outcome.Exception));
        Assert.Single(outcomes, outcome => outcome.Result is { WasIdempotentReplay: false });
        Assert.Single(outcomes, outcome => outcome.Result is { WasIdempotentReplay: true });

        await using var verificationContext = new ApplicationDbContext(dbOptions);
        var usageOperation = await verificationContext.UsageOperations.SingleAsync();
        var billableOperation = await verificationContext.CustomerBillableUsageOperations.SingleAsync();
        Assert.Equal(usageOperation.Id, billableOperation.UsageOperationId);
        Assert.Equal(1, (await verificationContext.UsageRecords.SingleAsync()).QuantityUsed);
    }

    private async Task<(Guid CompanyId, Guid PlanId, Guid SubscriptionId)> SeedAsync(long initialUsage)
    {
        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var periodStart = DateTimeOffset.UtcNow.AddHours(-1);
        var periodEnd = DateTimeOffset.UtcNow.AddHours(1);
        await using var context = new ApplicationDbContext(dbOptions);
        context.Companies.Add(new Company
        {
            Id = companyId,
            Name = "PostgreSQL customer billing test",
            Slug = $"customer-billing-{companyId:N}",
            NormalizedSlug = $"CUSTOMER-BILLING-{companyId:N}",
            Status = "Active",
            Email = $"{companyId:N}@billing.test",
            PhoneNumber = "0000000000",
            CountryCode = "US",
            TimeZone = "UTC"
        });
        context.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = $"PostgresBilling-{planId:N}",
            DisplayName = "Postgres billing test",
            Description = "PostgreSQL billing test plan",
            Entitlements =
            [
                new PlanEntitlement
                {
                    Id = Guid.NewGuid(),
                    Key = UsageMetric.AiTokens.ToQuotaEntitlementKey(),
                    IsEnabled = true,
                    QuotaLimit = 100,
                    IsUnlimited = false
                }
            ]
        });
        context.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = subscriptionId,
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = periodStart,
            CurrentPeriodEndUtc = periodEnd
        });
        context.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Metric = UsageMetric.AiTokens.ToStorageValue(),
            PeriodStartUtc = periodStart,
            PeriodEndUtc = periodEnd,
            QuantityUsed = initialUsage
        });
        await context.SaveChangesAsync();
        return (companyId, planId, subscriptionId);
    }

    private async Task<UsageConsumptionResult> ConsumeAsync(Guid companyId, string key)
    {
        await using var context = new ApplicationDbContext(dbOptions);
        var service = new SubscriptionEntitlementService(
            context,
            new StayFlow.Api.Services.CustomerBillingDeliveryCoordinator(
                new StayFlow.Api.Services.CustomerBillingDeliveryStore(context)),
            NullLogger<SubscriptionEntitlementService>.Instance);
        return await service.ConsumeQuotaAsync(
            companyId,
            UsageMetric.AiTokens,
            1,
            key,
            CancellationToken.None);
    }

    private static async Task<Outcome[]> RunConcurrentlyAsync(
        Func<Task<UsageConsumptionResult>> first,
        Func<Task<UsageConsumptionResult>> second)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new[]
        {
            CaptureAfterStartAsync(first, start.Task),
            CaptureAfterStartAsync(second, start.Task)
        };
        start.SetResult();
        return await Task.WhenAll(tasks);
    }

    private static async Task<Outcome> CaptureAfterStartAsync(
        Func<Task<UsageConsumptionResult>> operation,
        Task start)
    {
        await start;
        try
        {
            return new Outcome(await operation(), null);
        }
        catch (Exception exception)
        {
            return new Outcome(null, exception);
        }
    }

    private sealed record Outcome(UsageConsumptionResult? Result, Exception? Exception);
}
