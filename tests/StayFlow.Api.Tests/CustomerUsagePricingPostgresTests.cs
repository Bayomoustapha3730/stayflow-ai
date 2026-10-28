using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STAYFLOW_TEST_POSTGRES_CONNECTION")))
        {
            Skip = "NOT EXECUTED: STAYFLOW_TEST_POSTGRES_CONNECTION is not configured.";
        }
    }
}

public sealed class CustomerUsagePricingPostgresTests : IAsyncLifetime
{
    private readonly string databaseName = $"stayflow_customer_usage_pricing_{Guid.NewGuid():N}";
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
    public async Task Catalog_ConcurrentOverlappingPublicationsCannotBothSucceed()
    {
        var planId = SeedData.FreePlanId;
        var startsAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var catalog = new CustomerUsagePricingCatalog(dbOptions);

        var results = await Task.WhenAll(
            PublishAndCaptureAsync(catalog, planId, startsAt),
            PublishAndCaptureAsync(catalog, planId, startsAt.AddMinutes(1)));

        Assert.Single(results, result => result.Exception is null);
        Assert.Single(results, result => result.Exception is not null);
        await using var verificationContext = new ApplicationDbContext(dbOptions);
        Assert.Equal(1, await verificationContext.CustomerUsagePriceVersions.CountAsync());
    }

    [PostgresFact]
    public async Task Resolver_PreservesPrecisionAndResolvesExactEffectivePrice()
    {
        const decimal expectedPrice = 0.123456789012m;
        var planId = SeedData.StarterPlanId;
        var startsAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var catalog = new CustomerUsagePricingCatalog(dbOptions);
        var versionId = await catalog.PublishAsync(
            Draft(planId, startsAt, expectedPrice),
            CancellationToken.None);

        var result = await new CustomerUsagePricingResolver(dbOptions)
            .ResolveAsync(planId, UsageMetric.AiTokens, startsAt, CancellationToken.None);

        Assert.Equal(versionId, result.PricingVersionId);
        Assert.Equal(expectedPrice, result.UnitPrice);
        Assert.Equal("USD", result.Currency);
    }

    private static async Task<(Guid? Id, Exception? Exception)> PublishAndCaptureAsync(
        CustomerUsagePricingCatalog catalog,
        Guid planId,
        DateTimeOffset startsAt)
    {
        try
        {
            return (await catalog.PublishAsync(Draft(planId, startsAt, 1m), CancellationToken.None), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private static CustomerUsagePriceVersionDraft Draft(Guid planId, DateTimeOffset startsAt, decimal unitPrice) =>
        new(
            planId,
            UsageMetric.AiTokens,
            CustomerUsageBillingMode.PerUnitOverage,
            1_000_000m,
            unitPrice,
            "USD",
            startsAt,
            null);
}
