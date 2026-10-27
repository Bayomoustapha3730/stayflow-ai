using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.Data.Configurations;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Tests;

public sealed class AiProviderCallTelemetryStoreTests
{
    [Fact]
    public async Task RecordAsync_FirstUnknownObservationPersistsNullTokenCounts()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            await store.RecordAsync(Observation(callId, companyId), CancellationToken.None);

            var saved = await ReadCallAsync(context, callId);
            Assert.False(saved.UsageKnown);
            Assert.Null(saved.InputTokens);
            Assert.Null(saved.OutputTokens);
            Assert.Null(saved.TotalTokens);
        }
    }

    [Fact]
    public async Task RecordAsync_FirstKnownObservationPersistsTokenCounts()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            await store.RecordAsync(Observation(callId, companyId, new AiTokenUsage(100, 20, 120)), CancellationToken.None);

            var saved = await ReadCallAsync(context, callId);
            Assert.True(saved.UsageKnown);
            Assert.Equal(100, saved.InputTokens);
            Assert.Equal(20, saved.OutputTokens);
            Assert.Equal(120, saved.TotalTokens);
            Assert.Equal(AIProviderOutcome.Success, saved.Outcome);
        }
    }

    [Fact]
    public async Task RecordAsync_KnownZeroRemainsDistinctFromUnknown()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            await store.RecordAsync(Observation(callId, companyId, new AiTokenUsage(0, 0, 0)), CancellationToken.None);

            var saved = await ReadCallAsync(context, callId);
            Assert.True(saved.UsageKnown);
            Assert.Equal(0, saved.InputTokens);
            Assert.Equal(0, saved.OutputTokens);
            Assert.Equal(0, saved.TotalTokens);
        }
    }

    [Fact]
    public async Task RecordAsync_UnknownReplayIsIdempotent()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            var observation = Observation(callId, companyId);
            await store.RecordAsync(observation, CancellationToken.None);
            await store.RecordAsync(observation, CancellationToken.None);

            Assert.Equal(1, await context.AiProviderCallTelemetries.CountAsync());
            Assert.False((await ReadCallAsync(context, callId)).UsageKnown);
        }
    }

    [Fact]
    public async Task RecordAsync_UnknownThenKnownEnrichesOneRow()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            await store.RecordAsync(Observation(callId, companyId), CancellationToken.None);
            await store.RecordAsync(Observation(callId, companyId, new AiTokenUsage(100, 20, 120)), CancellationToken.None);

            Assert.Equal(1, await context.AiProviderCallTelemetries.CountAsync());
            var saved = await ReadCallAsync(context, callId);
            Assert.True(saved.UsageKnown);
            Assert.Equal(100, saved.InputTokens);
            Assert.Equal(20, saved.OutputTokens);
            Assert.Equal(120, saved.TotalTokens);
        }
    }

    [Fact]
    public async Task RecordAsync_KnownReplayAndUnknownReplayDoNotChangeKnownUsage()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            var known = Observation(callId, companyId, new AiTokenUsage(100, 20, 120));
            await store.RecordAsync(known, CancellationToken.None);
            await store.RecordAsync(known, CancellationToken.None);
            await store.RecordAsync(Observation(callId, companyId), CancellationToken.None);

            Assert.Equal(1, await context.AiProviderCallTelemetries.CountAsync());
            var saved = await ReadCallAsync(context, callId);
            Assert.True(saved.UsageKnown);
            Assert.Equal(100, saved.InputTokens);
            Assert.Equal(20, saved.OutputTokens);
            Assert.Equal(120, saved.TotalTokens);
        }
    }

    [Fact]
    public async Task RecordAsync_ConflictingKnownUsageRejectsWithoutMutation()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            await store.RecordAsync(
                Observation(callId, companyId, new AiTokenUsage(100, 20, 120)),
                CancellationToken.None);

            await Assert.ThrowsAsync<AiProviderCallTelemetryConflictException>(() => store.RecordAsync(
                Observation(callId, companyId, new AiTokenUsage(200, 40, 240)),
                CancellationToken.None));

            var saved = await ReadCallAsync(context, callId);
            Assert.Equal(100, saved.InputTokens);
            Assert.Equal(20, saved.OutputTokens);
            Assert.Equal(120, saved.TotalTokens);
            Assert.Equal(1, await context.AiProviderCallTelemetries.CountAsync());
        }
    }

    [Fact]
    public async Task RecordAsync_RejectsSameProviderCallIdAcrossCompaniesWithoutChangingOriginal()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var otherCompanyId = Guid.NewGuid();
            context.Companies.Add(CreateCompany(otherCompanyId));
            await context.SaveChangesAsync();

            var callId = ProviderCallId.New();
            await store.RecordAsync(Observation(callId, companyId, new AiTokenUsage(10, 2, 12)), CancellationToken.None);

            await Assert.ThrowsAsync<AiProviderCallTelemetryConflictException>(() => store.RecordAsync(
                Observation(callId, otherCompanyId, new AiTokenUsage(20, 4, 24)),
                CancellationToken.None));

            var saved = await ReadCallAsync(context, callId);
            Assert.Equal(companyId, saved.CompanyId);
            Assert.Equal(10, saved.InputTokens);
            Assert.Equal(2, saved.OutputTokens);
            Assert.Equal(12, saved.TotalTokens);
            Assert.Equal(1, await context.AiProviderCallTelemetries.CountAsync());
        }
    }

    [Fact]
    public async Task RecordAsync_RejectsChangedOuterIdWithoutChangingOriginal()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var firstOuterId = Guid.NewGuid();
            var secondOuterId = Guid.NewGuid();
            context.ConversationMessages.AddRange(
                new ConversationMessage { Id = firstOuterId, CompanyId = companyId },
                new ConversationMessage { Id = secondOuterId, CompanyId = companyId });
            await context.SaveChangesAsync();

            var callId = ProviderCallId.New();
            await store.RecordAsync(
                Observation(callId, companyId, outerType: OuterOperationType.GuestMessage, outerId: firstOuterId),
                CancellationToken.None);

            await Assert.ThrowsAsync<AiProviderCallTelemetryConflictException>(() => store.RecordAsync(
                Observation(callId, companyId, outerType: OuterOperationType.GuestMessage, outerId: secondOuterId),
                CancellationToken.None));

            var saved = await ReadCallAsync(context, callId);
            Assert.Equal(firstOuterId, saved.OuterOperationId);
            Assert.Equal(1, await context.AiProviderCallTelemetries.CountAsync());
        }
    }

    [Fact]
    public async Task RecordAsync_RejectsChangedOuterTypeWithoutChangingOriginal()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var guestMessageId = Guid.NewGuid();
            var copilotOperationId = Guid.NewGuid();
            context.ConversationMessages.Add(new ConversationMessage { Id = guestMessageId, CompanyId = companyId });
            context.CopilotOperations.Add(new CopilotOperation
            {
                Id = copilotOperationId,
                CompanyId = companyId,
                ConversationId = Guid.NewGuid(),
                ActorUserId = Guid.NewGuid(),
                OperationType = CopilotOperationType.CopilotSuggestion
            });
            await context.SaveChangesAsync();

            var callId = ProviderCallId.New();
            await store.RecordAsync(
                Observation(callId, companyId, outerType: OuterOperationType.GuestMessage, outerId: guestMessageId),
                CancellationToken.None);

            await Assert.ThrowsAsync<AiProviderCallTelemetryConflictException>(() => store.RecordAsync(
                Observation(callId, companyId, outerType: OuterOperationType.CopilotOperation, outerId: copilotOperationId),
                CancellationToken.None));

            var saved = await ReadCallAsync(context, callId);
            Assert.Equal(OuterOperationType.GuestMessage, saved.OuterOperationType);
            Assert.Equal(guestMessageId, saved.OuterOperationId);
        }
    }

    [Fact]
    public async Task RecordAsync_AllowsProviderEnrichmentAndRejectsChangedProvider()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            await store.RecordAsync(Observation(callId, companyId, provider: null), CancellationToken.None);
            await store.RecordAsync(Observation(callId, companyId, provider: "ProviderA"), CancellationToken.None);
            await store.RecordAsync(Observation(callId, companyId, provider: "ProviderA"), CancellationToken.None);

            await Assert.ThrowsAsync<AiProviderCallTelemetryConflictException>(() => store.RecordAsync(
                Observation(callId, companyId, provider: "ProviderB"),
                CancellationToken.None));

            var saved = await ReadCallAsync(context, callId);
            Assert.Equal("ProviderA", saved.Provider);
            Assert.Equal(1, await context.AiProviderCallTelemetries.CountAsync());
        }
    }

    [Fact]
    public async Task RecordAsync_AllowsModelEnrichmentAndRejectsChangedModel()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            await store.RecordAsync(Observation(callId, companyId, model: null), CancellationToken.None);
            await store.RecordAsync(Observation(callId, companyId, model: "model-a"), CancellationToken.None);
            await store.RecordAsync(Observation(callId, companyId, model: "model-a"), CancellationToken.None);

            await Assert.ThrowsAsync<AiProviderCallTelemetryConflictException>(() => store.RecordAsync(
                Observation(callId, companyId, model: "model-b"),
                CancellationToken.None));

            var saved = await ReadCallAsync(context, callId);
            Assert.Equal("model-a", saved.Model);
            Assert.Equal(1, await context.AiProviderCallTelemetries.CountAsync());
        }
    }

    [Fact]
    public async Task RecordAsync_OutcomeReplayIsIdempotentAndChangedOutcomeConflicts()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            var failed = Observation(callId, companyId, outcome: AIProviderOutcome.Failed);
            await store.RecordAsync(failed, CancellationToken.None);
            await store.RecordAsync(failed, CancellationToken.None);

            await Assert.ThrowsAsync<AiProviderCallTelemetryConflictException>(() => store.RecordAsync(
                Observation(callId, companyId, outcome: AIProviderOutcome.Success),
                CancellationToken.None));

            var saved = await ReadCallAsync(context, callId);
            Assert.Equal(AIProviderOutcome.Failed, saved.Outcome);
            Assert.Equal(1, await context.AiProviderCallTelemetries.CountAsync());
        }
    }

    [Fact]
    public async Task RecordAsync_IdempotentReplayDoesNotChangeAuditTimestamps()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            var observation = Observation(callId, companyId);
            await store.RecordAsync(observation, CancellationToken.None);
            var initial = await ReadCallAsync(context, callId);
            Assert.NotEqual(default, initial.CreatedAt);
            Assert.NotEqual(default, initial.UpdatedAt);

            await store.RecordAsync(observation, CancellationToken.None);

            var replayed = await ReadCallAsync(context, callId);
            Assert.Equal(initial.CreatedAt, replayed.CreatedAt);
            Assert.Equal(initial.UpdatedAt, replayed.UpdatedAt);
        }
    }

    [Fact]
    public async Task RecordAsync_ReconcilesCompatibleObservationAfterFinalUniqueRaceAttempt()
    {
        var companyId = Guid.NewGuid();
        var databaseName = $"ai-call-telemetry-final-race-{Guid.NewGuid():N}";
        var plainOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var interceptor = new FinalUniqueRaceInterceptor(plainOptions, companyId);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(interceptor)
            .Options;
        var callId = ProviderCallId.New();

        await using (var seedContext = new ApplicationDbContext(plainOptions))
        {
            seedContext.Companies.Add(CreateCompany(companyId));
            await seedContext.SaveChangesAsync();
        }

        var store = new AiProviderCallTelemetryStore(options);
        await store.RecordAsync(Observation(callId, companyId), CancellationToken.None);

        await using var verifyContext = new ApplicationDbContext(plainOptions);
        var saved = await verifyContext.AiProviderCallTelemetries.SingleAsync();
        Assert.Equal(3, interceptor.UniqueRaceAttempts);
        Assert.Equal(callId.Value, saved.ProviderCallId);
        Assert.Equal(companyId, saved.CompanyId);
        Assert.False(saved.UsageKnown);
        Assert.Equal(1, await verifyContext.AiProviderCallTelemetries.CountAsync());
    }

    [Fact]
    public async Task RecordAsync_DistinctProviderCallIdsPersistIndependently()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            await store.RecordAsync(Observation(ProviderCallId.New(), companyId), CancellationToken.None);
            await store.RecordAsync(Observation(ProviderCallId.New(), companyId), CancellationToken.None);

            Assert.Equal(2, await context.AiProviderCallTelemetries.CountAsync());
        }
    }

    [Fact]
    public async Task RecordAsync_PersistsMultipleProviderCallsForSameOuterOperation()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var outerId = Guid.NewGuid();
            context.CopilotOperations.Add(new CopilotOperation
            {
                Id = outerId,
                CompanyId = companyId,
                ConversationId = Guid.NewGuid(),
                ActorUserId = Guid.NewGuid(),
                OperationType = CopilotOperationType.CopilotSuggestion
            });
            await context.SaveChangesAsync();

            var firstCallId = ProviderCallId.New();
            var secondCallId = ProviderCallId.New();
            await store.RecordAsync(
                Observation(
                    firstCallId,
                    companyId,
                    new AiTokenUsage(10, 2, 12),
                    OuterOperationType.CopilotOperation,
                    outerId),
                CancellationToken.None);
            await store.RecordAsync(
                Observation(
                    secondCallId,
                    companyId,
                    outerType: OuterOperationType.CopilotOperation,
                    outerId: outerId,
                    outcome: AIProviderOutcome.Failed),
                CancellationToken.None);

            var saved = await context.AiProviderCallTelemetries
                .Where(call => call.CompanyId == companyId
                    && call.OuterOperationType == OuterOperationType.CopilotOperation
                    && call.OuterOperationId == outerId)
                .ToListAsync();

            Assert.Equal(2, saved.Count);
            Assert.Equal(2, saved.Select(call => call.ProviderCallId).Distinct(StringComparer.Ordinal).Count());

            var first = Assert.Single(saved, call => call.ProviderCallId == firstCallId.Value);
            Assert.True(first.UsageKnown);
            Assert.Equal(10, first.InputTokens);
            Assert.Equal(2, first.OutputTokens);
            Assert.Equal(12, first.TotalTokens);
            Assert.Equal(AIProviderOutcome.Success, first.Outcome);

            var second = Assert.Single(saved, call => call.ProviderCallId == secondCallId.Value);
            Assert.False(second.UsageKnown);
            Assert.Null(second.InputTokens);
            Assert.Null(second.OutputTokens);
            Assert.Null(second.TotalTokens);
            Assert.Equal(AIProviderOutcome.Failed, second.Outcome);
        }
    }

    [Fact]
    public async Task RecordAsync_FailedProviderCallPersistsUnknownUsage()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var callId = ProviderCallId.New();
            await store.RecordAsync(
                Observation(callId, companyId, outcome: AIProviderOutcome.Failed),
                CancellationToken.None);

            var saved = await ReadCallAsync(context, callId);
            Assert.Equal(AIProviderOutcome.Failed, saved.Outcome);
            Assert.False(saved.UsageKnown);
            Assert.Null(saved.InputTokens);
            Assert.Null(saved.OutputTokens);
            Assert.Null(saved.TotalTokens);
        }
    }

    [Fact]
    public async Task RecordAsync_PreservesCompanyAndOuterOperationIdentity()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var outerId = Guid.NewGuid();
            context.CopilotOperations.Add(new CopilotOperation
            {
                Id = outerId,
                CompanyId = companyId,
                ConversationId = Guid.NewGuid(),
                ActorUserId = Guid.NewGuid(),
                OperationType = CopilotOperationType.CopilotSuggestion
            });
            await context.SaveChangesAsync();

            var callId = ProviderCallId.New();
            await store.RecordAsync(
                Observation(callId, companyId, new AiTokenUsage(1, 2, 3), OuterOperationType.CopilotOperation, outerId),
                CancellationToken.None);

            var saved = await ReadCallAsync(context, callId);
            Assert.Equal(companyId, saved.CompanyId);
            Assert.Equal(OuterOperationType.CopilotOperation, saved.OuterOperationType);
            Assert.Equal(outerId, saved.OuterOperationId);
        }
    }

    [Fact]
    public async Task RecordAsync_RejectsOuterIdentityOwnedByAnotherCompany()
    {
        var (context, store, companyId) = await CreateStoreAsync();
        await using (context)
        {
            var otherCompanyId = Guid.NewGuid();
            context.Companies.Add(CreateCompany(otherCompanyId));
            var outerId = Guid.NewGuid();
            context.CopilotOperations.Add(new CopilotOperation
            {
                Id = outerId,
                CompanyId = otherCompanyId,
                ConversationId = Guid.NewGuid(),
                ActorUserId = Guid.NewGuid(),
                OperationType = CopilotOperationType.CopilotSuggestion
            });
            await context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => store.RecordAsync(
                Observation(ProviderCallId.New(), companyId, outerType: OuterOperationType.CopilotOperation, outerId: outerId),
                CancellationToken.None));

            Assert.Empty(await context.AiProviderCallTelemetries.ToListAsync());
        }
    }

    [Fact]
    public async Task RelationalModel_EnforcesGlobalProviderCallIdUniqueness()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var companyId = Guid.NewGuid();
        context.Companies.Add(CreateCompany(companyId));
        await context.SaveChangesAsync();

        var callId = ProviderCallId.New().Value;
        context.AiProviderCallTelemetries.Add(new AiProviderCallTelemetry
        {
            ProviderCallId = callId,
            CompanyId = companyId,
            UsageKnown = false,
            Outcome = AIProviderOutcome.Failed
        });
        await context.SaveChangesAsync();

        context.AiProviderCallTelemetries.Add(new AiProviderCallTelemetry
        {
            ProviderCallId = callId,
            CompanyId = companyId,
            UsageKnown = false,
            Outcome = AIProviderOutcome.Failed
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task RecordAsync_DoesNotSaveUnrelatedCallerContextChanges()
    {
        var companyId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-call-telemetry-isolation-{Guid.NewGuid():N}")
            .Options;
        await using var callerContext = new ApplicationDbContext(options);
        callerContext.Companies.Add(CreateCompany(companyId));
        await callerContext.SaveChangesAsync();
        var trackedCompany = await callerContext.Companies.SingleAsync(company => company.Id == companyId);
        trackedCompany.Name = "Unsaved caller change";

        await new AiProviderCallTelemetryStore(options).RecordAsync(
            Observation(ProviderCallId.New(), companyId),
            CancellationToken.None);

        await using var verificationContext = new ApplicationDbContext(options);
        var persistedCompany = await verificationContext.Companies.AsNoTracking()
            .SingleAsync(company => company.Id == companyId);
        Assert.Equal("Telemetry test company", persistedCompany.Name);
        Assert.Single(await verificationContext.AiProviderCallTelemetries.ToListAsync());
    }

    private static AiProviderCallObservation Observation(
        ProviderCallId callId,
        Guid companyId,
        AiTokenUsage? usage = null,
        OuterOperationType? outerType = null,
        Guid? outerId = null,
        string? provider = "TestProvider",
        string? model = "test-model",
        AIProviderOutcome outcome = AIProviderOutcome.Success)
    {
        return new AiProviderCallObservation(callId, companyId, outerType, outerId, provider, model, usage, outcome);
    }

    private static async Task<AiProviderCallTelemetry> ReadCallAsync(ApplicationDbContext context, ProviderCallId callId)
    {
        return await context.AiProviderCallTelemetries.SingleAsync(call => call.ProviderCallId == callId.Value);
    }

    private static async Task<(ApplicationDbContext Context, AiProviderCallTelemetryStore Store, Guid CompanyId)> CreateStoreAsync()
    {
        var companyId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-call-telemetry-{Guid.NewGuid():N}")
            .Options;
        var context = new ApplicationDbContext(options);
        context.Companies.Add(CreateCompany(companyId));
        await context.SaveChangesAsync();
        return (context, new AiProviderCallTelemetryStore(options), companyId);
    }

    private static Company CreateCompany(Guid id)
    {
        return new Company
        {
            Id = id,
            Name = "Telemetry test company",
            Slug = $"telemetry-{id:N}",
            NormalizedSlug = $"TELEMETRY-{id:N}",
            Status = "Active",
            Email = "telemetry@example.test",
            PhoneNumber = "0000000000",
            CountryCode = "US",
            TimeZone = "UTC"
        };
    }

    private sealed class FinalUniqueRaceInterceptor(
        DbContextOptions<ApplicationDbContext> plainOptions,
        Guid companyId) : SaveChangesInterceptor
    {
        public int UniqueRaceAttempts { get; private set; }
        private int SavingChangesCalls { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SavingChangesCalls++;
            if (SavingChangesCalls == 3)
            {
                await using var winnerContext = new ApplicationDbContext(plainOptions);
                winnerContext.AiProviderCallTelemetries.Add(new AiProviderCallTelemetry
                {
                    ProviderCallId = ((AiProviderCallTelemetry)eventData.Context!
                        .ChangeTracker.Entries<AiProviderCallTelemetry>().Single().Entity).ProviderCallId,
                    CompanyId = companyId,
                    Provider = "TestProvider",
                    Model = "test-model",
                    UsageKnown = false,
                    Outcome = AIProviderOutcome.Success
                });
                await winnerContext.SaveChangesAsync(cancellationToken);
            }

            if (SavingChangesCalls > 3)
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
                    constraintName: AiProviderCallTelemetryConfiguration.ProviderCallIdUniqueIndexName));
        }
    }
}
