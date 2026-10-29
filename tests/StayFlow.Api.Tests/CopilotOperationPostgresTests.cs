using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CopilotOperationPostgresTests : IAsyncLifetime
{
    private const string TestPostgresConnectionVariable = "STAYFLOW_TEST_POSTGRES_CONNECTION";
    private const string DefaultConnectionVariable = "ConnectionStrings__DefaultConnection";
    private readonly string databaseName = $"stayflow_copilot_operation_test_{Guid.NewGuid():N}";
    private string maintenanceConnectionString = string.Empty;
    private DbContextOptions<ApplicationDbContext> dbOptions = null!;
    private Guid companyId;
    private Guid actorUserId;
    private Guid conversationId;
    private DateTimeOffset periodStart;

    public async Task InitializeAsync()
    {
        maintenanceConnectionString = ResolveMaintenanceConnectionString();
        await using (var maintenanceConnection = new NpgsqlConnection(maintenanceConnectionString))
        {
            await maintenanceConnection.OpenAsync();
            await using var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", maintenanceConnection);
            await createCommand.ExecuteNonQueryAsync();
        }

        var databaseBuilder = new NpgsqlConnectionStringBuilder(maintenanceConnectionString) { Database = databaseName };
        databaseBuilder.Pooling = false;
        dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(databaseBuilder.ConnectionString)
            .Options;
        await using var schemaContext = new ApplicationDbContext(dbOptions);
        await schemaContext.Database.EnsureCreatedAsync();
        await SeedAsync(schemaContext);
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
    public async Task BeginCopilotOperationAsync_ConcurrentSameIdentityHasOneDispatchOwner()
    {
        var operationId = Guid.NewGuid();
        var fingerprint = CopilotOperationFingerprint.GeneratedReply(conversationId, "professional", "Draft", "Guidance");
        using var startGate = new ManualResetEventSlim(false);
        var attempts = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await using var db = new ApplicationDbContext(dbOptions);
            var service = NewService(db);
            startGate.Wait();
            return await service.BeginCopilotOperationAsync(
                companyId, operationId, conversationId, actorUserId,
                CopilotOperationType.CopilotGeneratedReply, fingerprint, CancellationToken.None);
        })).ToArray();

        startGate.Set();
        var outcomes = await Task.WhenAll(attempts);

        Assert.Single(outcomes, outcome => outcome.Disposition == CopilotOperationExecutionDisposition.DispatchOwned);
        Assert.Single(outcomes, outcome => outcome.Disposition == CopilotOperationExecutionDisposition.InProgress);
        await using var verification = new ApplicationDbContext(dbOptions);
        var operation = await verification.CopilotOperations.SingleAsync();
        Assert.Equal(CopilotOperationStatus.InProgress, operation.Status);
        Assert.Equal(fingerprint, operation.SemanticRequestFingerprint);
        Assert.Equal(1, await verification.UsageOperations.CountAsync(item =>
            item.CompanyId == companyId && item.Metric == UsageMetric.AiRequests.ToStorageValue()));
    }

    [Fact]
    public async Task BeginCopilotOperationAsync_ConcurrentSemanticMismatchConflictsWithoutSecondQuota()
    {
        var operationId = Guid.NewGuid();
        var fingerprint = CopilotOperationFingerprint.GeneratedReply(conversationId, "professional", "Draft", "Guidance");
        var changedFingerprint = CopilotOperationFingerprint.GeneratedReply(conversationId, "professional", "Different draft", "Guidance");
        var first = await BeginAsync(operationId, fingerprint, CopilotOperationType.CopilotGeneratedReply);

        Assert.Equal(CopilotOperationExecutionDisposition.DispatchOwned, first.Disposition);
        await using var db = new ApplicationDbContext(dbOptions);
        var service = NewService(db);

        await Assert.ThrowsAsync<ConflictException>(() => service.BeginCopilotOperationAsync(
            companyId, operationId, conversationId, actorUserId,
            CopilotOperationType.CopilotGeneratedReply, changedFingerprint, CancellationToken.None));

        Assert.Equal(1, await db.UsageOperations.CountAsync(item =>
            item.CompanyId == companyId && item.Metric == UsageMetric.AiRequests.ToStorageValue()));
    }

    [Fact]
    public async Task BeginCopilotOperationAsync_CompletedResultSurvivesNewServiceInstance()
    {
        var operationId = Guid.NewGuid();
        var fingerprint = CopilotOperationFingerprint.Suggestions(conversationId, "friendly");
        Assert.Equal(CopilotOperationExecutionDisposition.DispatchOwned, (await BeginAsync(operationId, fingerprint)).Disposition);
        await using (var ownerContext = new ApplicationDbContext(dbOptions))
        {
            await NewService(ownerContext).CompleteCopilotOperationAsync(
                companyId, operationId, "{\"suggestedReplies\":[\"persisted\"]}", CancellationToken.None);
        }

        var replay = await BeginAsync(operationId, fingerprint);

        Assert.Equal(CopilotOperationExecutionDisposition.Completed, replay.Disposition);
        Assert.Equal("{\"suggestedReplies\":[\"persisted\"]}", replay.BusinessResultJson);
        await using var verification = new ApplicationDbContext(dbOptions);
        Assert.Equal(1, await verification.UsageOperations.CountAsync(item =>
            item.CompanyId == companyId && item.Metric == UsageMetric.AiRequests.ToStorageValue()));
    }

    [Fact]
    public async Task BeginCopilotOperationAsync_IndeterminateReplayNeverReclaimsDispatch()
    {
        var operationId = Guid.NewGuid();
        var fingerprint = CopilotOperationFingerprint.WorkspaceDraft(conversationId, "professional", "Instruction");
        Assert.Equal(CopilotOperationExecutionDisposition.DispatchOwned,
            (await BeginAsync(operationId, fingerprint, CopilotOperationType.WorkspaceDraft)).Disposition);
        await using (var ownerContext = new ApplicationDbContext(dbOptions))
        {
            await NewService(ownerContext).MarkCopilotOperationIndeterminateAsync(
                companyId, operationId, CancellationToken.None);
        }

        var replay = await BeginAsync(operationId, fingerprint, CopilotOperationType.WorkspaceDraft);

        Assert.Equal(CopilotOperationExecutionDisposition.Indeterminate, replay.Disposition);
        await using var verification = new ApplicationDbContext(dbOptions);
        Assert.Equal(1, await verification.UsageOperations.CountAsync(item =>
            item.CompanyId == companyId && item.Metric == UsageMetric.AiRequests.ToStorageValue()));
    }

    private async Task<CopilotOperationExecutionStart> BeginAsync(
        Guid operationId,
        string fingerprint,
        CopilotOperationType operationType = CopilotOperationType.CopilotSuggestion)
    {
        await using var db = new ApplicationDbContext(dbOptions);
        return await NewService(db).BeginCopilotOperationAsync(
            companyId, operationId, conversationId, actorUserId,
            operationType, fingerprint, CancellationToken.None);
    }

    private static SubscriptionEntitlementService NewService(ApplicationDbContext db)
        => new(
            db,
            new CustomerBillingDeliveryCoordinator(new CustomerBillingDeliveryStore(db)),
            NullLogger<SubscriptionEntitlementService>.Instance);

    private async Task SeedAsync(ApplicationDbContext db)
    {
        companyId = Guid.NewGuid();
        actorUserId = Guid.NewGuid();
        conversationId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        periodStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        db.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Replay Test Company",
            Slug = $"replay-{companyId:N}",
            NormalizedSlug = $"REPLAY-{companyId:N}",
            Status = "Active",
            Email = $"{companyId:N}@test.local",
            PhoneNumber = "+254700000000",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        db.Users.Add(new User
        {
            Id = actorUserId,
            CompanyId = companyId,
            FullName = "Replay Test Host",
            Email = $"{actorUserId:N}@test.local",
            NormalizedEmail = $"{actorUserId:N}@TEST.LOCAL",
            Role = "Admin",
            PasswordHash = "test-hash",
            IsActive = true
        });
        db.Guests.Add(new Guest
        {
            Id = guestId,
            CompanyId = companyId,
            FirstName = "Replay",
            LastName = "Guest",
            IsActive = true
        });
        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            CompanyId = companyId,
            GuestId = guestId,
            Channel = StayFlow.Api.DTOs.ReservationContext.GuestChannel.Web,
            Status = ConversationStatus.Open,
            StartedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow
        });
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = $"ReplayTest-{companyId:N}",
            DisplayName = "Replay Test",
            Description = "Copilot replay concurrency test",
            IsActive = true,
            Entitlements =
            [
                new PlanEntitlement
                {
                    Id = Guid.NewGuid(),
                    Key = UsageMetric.AiRequests.ToQuotaEntitlementKey(),
                    IsEnabled = true,
                    QuotaLimit = 100,
                    IsUnlimited = false,
                    Unit = "requests"
                }
            ]
        });
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = periodStart,
            CurrentPeriodEndUtc = periodStart.AddMonths(1).AddTicks(-1)
        });
        await db.SaveChangesAsync();
    }

    private static string ResolveMaintenanceConnectionString()
    {
        var explicitConnection = Environment.GetEnvironmentVariable(TestPostgresConnectionVariable);
        if (!string.IsNullOrWhiteSpace(explicitConnection))
        {
            return explicitConnection;
        }

        var defaultConnection = Environment.GetEnvironmentVariable(DefaultConnectionVariable);
        if (!string.IsNullOrWhiteSpace(defaultConnection))
        {
            return new NpgsqlConnectionStringBuilder(defaultConnection) { Database = "postgres" }.ConnectionString;
        }

        throw new InvalidOperationException(
            $"{TestPostgresConnectionVariable} or {DefaultConnectionVariable} must point to a PostgreSQL test cluster.");
    }
}
