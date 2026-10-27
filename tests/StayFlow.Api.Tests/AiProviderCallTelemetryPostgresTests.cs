using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Tests;

public sealed class AiProviderCallTelemetryPostgresTests : IAsyncLifetime
{
    private const string TestPostgresConnectionVariable = "STAYFLOW_TEST_POSTGRES_CONNECTION";
    private readonly string databaseName = $"stayflow_ai_telemetry_test_{Guid.NewGuid():N}";
    private string maintenanceConnectionString = string.Empty;
    private string testDatabaseConnectionString = string.Empty;
    private DbContextOptions<ApplicationDbContext> dbOptions = null!;

    public async Task InitializeAsync()
    {
        var testConnection = Environment.GetEnvironmentVariable(TestPostgresConnectionVariable);
        if (string.IsNullOrWhiteSpace(testConnection))
        {
            throw new InvalidOperationException(
                $"{TestPostgresConnectionVariable} must point to a disposable PostgreSQL test cluster.");
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
        testDatabaseConnectionString = testDatabaseBuilder.ConnectionString;
        dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(testDatabaseConnectionString)
            .Options;

        await using var schemaContext = new ApplicationDbContext(dbOptions);
        await schemaContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
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

    [Fact]
    public async Task RecordAsync_ConcurrentFirstWriteCreatesOneProviderCall()
    {
        var companyId = await SeedCompanyAsync();
        var callId = ProviderCallId.New();
        var observation = Observation(callId, companyId);

        await Task.WhenAll(
            RecordWithNewContextAsync(observation),
            RecordWithNewContextAsync(observation));

        await using var verificationContext = new ApplicationDbContext(dbOptions);
        Assert.Equal(1, await verificationContext.AiProviderCallTelemetries.CountAsync());
    }

    [Fact]
    public async Task RecordAsync_ConcurrentUnknownAndKnownReplayKeepsKnownUsage()
    {
        var companyId = await SeedCompanyAsync();
        var callId = ProviderCallId.New();
        await RecordWithNewContextAsync(Observation(callId, companyId));

        await Task.WhenAll(
            RecordWithNewContextAsync(Observation(callId, companyId)),
            RecordWithNewContextAsync(Observation(callId, companyId, new AiTokenUsage(100, 20, 120))));

        await using var verificationContext = new ApplicationDbContext(dbOptions);
        var saved = await verificationContext.AiProviderCallTelemetries.SingleAsync();
        Assert.True(saved.UsageKnown);
        Assert.Equal(100, saved.InputTokens);
        Assert.Equal(20, saved.OutputTokens);
        Assert.Equal(120, saved.TotalTokens);
    }

    [Fact]
    public async Task RecordAsync_ConcurrentConflictingKnownObservationsNeverSumOrOverwriteSilently()
    {
        var companyId = await SeedCompanyAsync();
        var callId = ProviderCallId.New();
        await RecordWithNewContextAsync(Observation(callId, companyId));

        var results = await Task.WhenAll(
            RecordAndCaptureConflictAsync(Observation(callId, companyId, new AiTokenUsage(100, 20, 120))),
            RecordAndCaptureConflictAsync(Observation(callId, companyId, new AiTokenUsage(200, 40, 240))));

        Assert.Single(results, conflicted => !conflicted);
        Assert.Single(results, conflicted => conflicted);

        await using var verificationContext = new ApplicationDbContext(dbOptions);
        var saved = await verificationContext.AiProviderCallTelemetries.SingleAsync();
        Assert.True(saved.UsageKnown);
        Assert.Contains(saved.InputTokens, new long?[] { 100, 200 });
        Assert.Contains(saved.OutputTokens, new long?[] { 20, 40 });
        Assert.Contains(saved.TotalTokens, new long?[] { 120, 240 });
        Assert.NotEqual(300, saved.InputTokens);
        Assert.NotEqual(60, saved.OutputTokens);
        Assert.NotEqual(360, saved.TotalTokens);
    }

    [Fact]
    public async Task RecordAsync_ConcurrentCrossTenantCallIdHasOneOwnerAndOneConflict()
    {
        var companyA = await SeedCompanyAsync();
        var companyB = await SeedCompanyAsync();
        var callId = ProviderCallId.New();

        var results = await Task.WhenAll(
            RecordAndCaptureConflictAsync(Observation(callId, companyA, new AiTokenUsage(10, 2, 12))),
            RecordAndCaptureConflictAsync(Observation(callId, companyB, new AiTokenUsage(20, 4, 24))));

        Assert.Single(results, conflicted => !conflicted);
        Assert.Single(results, conflicted => conflicted);

        await using var verificationContext = new ApplicationDbContext(dbOptions);
        var saved = await verificationContext.AiProviderCallTelemetries.SingleAsync();
        Assert.Contains(saved.CompanyId, new[] { companyA, companyB });
        Assert.Equal(1, await verificationContext.AiProviderCallTelemetries.CountAsync());
        Assert.True(
            (saved.CompanyId == companyA && saved.InputTokens == 10 && saved.OutputTokens == 2 && saved.TotalTokens == 12)
            || (saved.CompanyId == companyB && saved.InputTokens == 20 && saved.OutputTokens == 4 && saved.TotalTokens == 24));
    }

    [Fact]
    public async Task RecordAsync_ReconcilesAfterFinalUniqueRaceAttemptOnPostgres()
    {
        var companyId = await SeedCompanyAsync();
        var raceInterceptor = new FinalUniqueRaceInterceptor(dbOptions, companyId);
        var interceptedOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(testDatabaseConnectionString)
            .AddInterceptors(raceInterceptor)
            .Options;
        var callId = ProviderCallId.New();

        await new AiProviderCallTelemetryStore(interceptedOptions)
            .RecordAsync(Observation(callId, companyId), CancellationToken.None);

        await using var verificationContext = new ApplicationDbContext(dbOptions);
        var saved = await verificationContext.AiProviderCallTelemetries.SingleAsync();
        Assert.Equal(3, raceInterceptor.UniqueRaceAttempts);
        Assert.Equal(callId.Value, saved.ProviderCallId);
        Assert.Equal(companyId, saved.CompanyId);
        Assert.False(saved.UsageKnown);
        Assert.Equal(1, await verificationContext.AiProviderCallTelemetries.CountAsync());
    }

    private async Task<Guid> SeedCompanyAsync()
    {
        var companyId = Guid.NewGuid();
        await using var context = new ApplicationDbContext(dbOptions);
        context.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Telemetry concurrency company",
            Slug = $"telemetry-{companyId:N}",
            NormalizedSlug = $"TELEMETRY-{companyId:N}",
            Status = "Active",
            Email = "telemetry@example.test",
            PhoneNumber = "0000000000",
            CountryCode = "US",
            TimeZone = "UTC"
        });
        await context.SaveChangesAsync();
        return companyId;
    }

    private async Task RecordWithNewContextAsync(AiProviderCallObservation observation)
    {
        await new AiProviderCallTelemetryStore(dbOptions).RecordAsync(observation, CancellationToken.None);
    }

    private async Task<bool> RecordAndCaptureConflictAsync(AiProviderCallObservation observation)
    {
        try
        {
            await RecordWithNewContextAsync(observation);
            return false;
        }
        catch (AiProviderCallTelemetryConflictException)
        {
            return true;
        }
    }

    private static AiProviderCallObservation Observation(
        ProviderCallId callId,
        Guid companyId,
        AiTokenUsage? usage = null)
    {
        return new AiProviderCallObservation(
            callId,
            companyId,
            null,
            null,
            "OpenAI",
            "gpt-test",
            usage,
            AIProviderOutcome.Success);
    }

    private sealed class FinalUniqueRaceInterceptor(
        DbContextOptions<ApplicationDbContext> plainOptions,
        Guid companyId) : SaveChangesInterceptor
    {
        private int saveChangesCalls;
        public int UniqueRaceAttempts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            saveChangesCalls++;
            if (saveChangesCalls == 3)
            {
                var call = eventData.Context!.ChangeTracker.Entries<AiProviderCallTelemetry>().Single().Entity;
                await using var winnerContext = new ApplicationDbContext(plainOptions);
                winnerContext.AiProviderCallTelemetries.Add(new AiProviderCallTelemetry
                {
                    ProviderCallId = call.ProviderCallId,
                    CompanyId = companyId,
                    Provider = call.Provider,
                    Model = call.Model,
                    UsageKnown = false,
                    Outcome = call.Outcome
                });
                await winnerContext.SaveChangesAsync(cancellationToken);
            }

            if (saveChangesCalls > 3)
            {
                return result;
            }

            UniqueRaceAttempts++;
            throw new DbUpdateException(
                "Simulated PostgreSQL ProviderCallId uniqueness race.",
                new PostgresException(
                    "duplicate key value violates unique constraint",
                    "ERROR",
                    "ERROR",
                    PostgresErrorCodes.UniqueViolation,
                    constraintName: StayFlow.Api.Data.Configurations.AiProviderCallTelemetryConfiguration.ProviderCallIdUniqueIndexName));
        }
    }
}
