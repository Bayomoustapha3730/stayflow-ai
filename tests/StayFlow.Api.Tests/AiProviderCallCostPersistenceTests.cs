using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Tests;

public sealed class AiProviderCallCostPersistenceTests
{
    [Fact]
    public async Task PersistCostAsync_UnknownEnrichesToCalculatedAndCalculatedCannotBeOverwritten()
    {
        var (options, companyId, store) = await CreateStoreAsync();
        var versionId = await new AiProviderPricingCatalog(options).PublishAsync(
            new AiProviderPricingVersionDraft(
                "OpenAI",
                "synthetic-model",
                DateTimeOffset.UtcNow.AddDays(-1),
                null,
                "USD",
                1m,
                1m,
                1_000_000),
            CancellationToken.None);
        var callId = ProviderCallId.New();
        await store.RecordAsync(Observation(callId, companyId), CancellationToken.None);

        await store.PersistCostAsync(
            callId,
            companyId,
            new AiProviderCostResult(AiProviderCostState.Unknown, AiProviderCostReason.MissingTokenUsage),
            CancellationToken.None);
        var calculated = new AiProviderCostResult(
            AiProviderCostState.Calculated,
            AiProviderCostReason.Calculated,
            versionId,
            0m,
            "USD");
        await store.PersistCostAsync(callId, companyId, calculated, CancellationToken.None);
        await store.PersistCostAsync(callId, companyId, calculated, CancellationToken.None);

        await Assert.ThrowsAsync<AiProviderCallTelemetryConflictException>(() =>
            store.PersistCostAsync(
                callId,
                companyId,
                new AiProviderCostResult(
                    AiProviderCostState.Calculated,
                    AiProviderCostReason.Calculated,
                    versionId,
                    0.01m,
                    "USD"),
                CancellationToken.None));
        await Assert.ThrowsAsync<AiProviderCallTelemetryConflictException>(() =>
            store.PersistCostAsync(
                callId,
                companyId,
                new AiProviderCostResult(AiProviderCostState.Unknown, AiProviderCostReason.MissingTokenUsage),
                CancellationToken.None));

        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.AiProviderCallTelemetries.AsNoTracking().SingleAsync();
        Assert.Equal(AiProviderCostState.Calculated, saved.CostState);
        Assert.Equal(AiProviderCostReason.Calculated, saved.CostReason);
        Assert.Equal(versionId, saved.PricingVersionId);
        Assert.Equal(0m, saved.ProviderCostAmount);
        Assert.Equal("USD", saved.ProviderCostCurrency);
    }

    [Fact]
    public async Task PersistCostAsync_UnknownAndNotApplicableDoNotPersistMonetaryAmounts()
    {
        var (options, companyId, store) = await CreateStoreAsync();
        var unknownCallId = ProviderCallId.New();
        var notApplicableCallId = ProviderCallId.New();
        await store.RecordAsync(Observation(unknownCallId, companyId), CancellationToken.None);
        await store.RecordAsync(Observation(notApplicableCallId, companyId), CancellationToken.None);

        await store.PersistCostAsync(
            unknownCallId,
            companyId,
            new AiProviderCostResult(AiProviderCostState.Unknown, AiProviderCostReason.UnknownModel),
            CancellationToken.None);
        await store.PersistCostAsync(
            notApplicableCallId,
            companyId,
            new AiProviderCostResult(AiProviderCostState.NotApplicable, AiProviderCostReason.NonBillableProvider),
            CancellationToken.None);

        await using var verify = new ApplicationDbContext(options);
        var calls = await verify.AiProviderCallTelemetries.AsNoTracking().ToListAsync();
        Assert.All(calls, call =>
        {
            Assert.Null(call.ProviderCostAmount);
            Assert.Null(call.ProviderCostCurrency);
        });
        Assert.Contains(calls, call => call.CostState == AiProviderCostState.Unknown
            && call.CostReason == AiProviderCostReason.UnknownModel);
        Assert.Contains(calls, call => call.CostState == AiProviderCostState.NotApplicable
            && call.CostReason == AiProviderCostReason.NonBillableProvider);
    }

    [Fact]
    public async Task RelationalModel_RejectsCalculatedCostWithoutPricingAndAmount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var company = CreateCompany();
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        context.AiProviderCallTelemetries.Add(new AiProviderCallTelemetry
        {
            ProviderCallId = ProviderCallId.New().Value,
            CompanyId = company.Id,
            CostState = AiProviderCostState.Calculated,
            CostReason = AiProviderCostReason.Calculated
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static AiProviderCallObservation Observation(ProviderCallId callId, Guid companyId) =>
        new(
            callId,
            companyId,
            null,
            null,
            "OpenAI",
            "requested-model",
            null,
            AIProviderOutcome.Success,
            ResolvedModel: "synthetic-model");

    private static async Task<(DbContextOptions<ApplicationDbContext> Options, Guid CompanyId, AiProviderCallTelemetryStore Store)>
        CreateStoreAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-call-cost-{Guid.NewGuid():N}")
            .Options;
        var company = CreateCompany();
        await using (var context = new ApplicationDbContext(options))
        {
            context.Companies.Add(company);
            await context.SaveChangesAsync();
        }

        return (options, company.Id, new AiProviderCallTelemetryStore(options));
    }

    private static Company CreateCompany()
    {
        var id = Guid.NewGuid();
        return new Company
        {
            Id = id,
            Name = "Cost test company",
            Slug = $"cost-{id:N}",
            NormalizedSlug = $"COST-{id:N}",
            Status = "Active",
            Email = "cost@example.test",
            PhoneNumber = "0000000000",
            CountryCode = "US",
            TimeZone = "UTC"
        };
    }
}
