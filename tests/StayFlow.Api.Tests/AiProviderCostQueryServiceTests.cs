using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class AiProviderCostQueryServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_AggregatesPersistedStatesWithinHalfOpenRange()
    {
        var (options, companyId) = CreateContextOptions();
        await SeedAsync(options,
            Call(companyId, "OpenAI", "resolved-model", Utc(1), AiProviderCostState.Calculated,
                AiProviderCostReason.Calculated, 0.123456789012m),
            Call(companyId, "OpenAI", "resolved-model", Utc(12), AiProviderCostState.Unknown,
                AiProviderCostReason.UnknownModel),
            Call(companyId, "OpenAI", "resolved-model", Utc(18), AiProviderCostState.NotApplicable,
                AiProviderCostReason.NonBillableProvider),
            Call(companyId, "OpenAI", "resolved-model", Utc(24), AiProviderCostState.Calculated,
                AiProviderCostReason.Calculated, 9m),
            Call(companyId, "OpenAI", "resolved-model", Utc(0).AddTicks(-1), AiProviderCostState.Calculated,
                AiProviderCostReason.Calculated, 9m),
            Call(companyId, "OpenAI", "resolved-model", null, AiProviderCostState.Unknown,
                AiProviderCostReason.MissingDispatchTimestamp));

        await using var context = new ApplicationDbContext(options);
        var result = await new AiProviderCostQueryService(context).GetSummaryAsync(
            new AiProviderCostQuery(Utc(0), Utc(24), companyId, "OpenAI", "resolved-model"),
            CancellationToken.None);

        Assert.Equal(3, result.TotalCalls);
        Assert.Equal(1, result.CalculatedCalls);
        Assert.Equal(1, result.UnknownCalls);
        Assert.Equal(1, result.NotApplicableCalls);
        Assert.Equal(1, result.MissingDispatchTimestampCalls);
        Assert.Equal(0.123456789012m, result.CalculatedCostAmount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal([new AiProviderCostUnknownReasonCount(AiProviderCostReason.UnknownModel, 1)], result.UnknownByReason);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsCalculatedZeroCostAsCalculated()
    {
        var (options, companyId) = CreateContextOptions();
        await SeedAsync(options,
            Call(companyId, "OpenAI", "resolved-model", Utc(1), AiProviderCostState.Calculated,
                AiProviderCostReason.Calculated, 0m));

        await using var context = new ApplicationDbContext(options);
        var result = await new AiProviderCostQueryService(context).GetSummaryAsync(
            new AiProviderCostQuery(Utc(0), Utc(24)),
            CancellationToken.None);

        Assert.Equal(1, result.TotalCalls);
        Assert.Equal(1, result.CalculatedCalls);
        Assert.Equal(0, result.UnknownCalls);
        Assert.Equal(0m, result.CalculatedCostAmount);
    }

    [Fact]
    public async Task GetSummaryAsync_AggregatesAcrossCompaniesAndAppliesCompanyFilter()
    {
        var (options, firstCompanyId) = CreateContextOptions();
        var secondCompanyId = Guid.NewGuid();
        await SeedAsync(options,
            Call(firstCompanyId, "OpenAI", "model-a", Utc(1), AiProviderCostState.Calculated,
                AiProviderCostReason.Calculated, 1.25m),
            Call(secondCompanyId, "OpenAI", "model-b", Utc(2), AiProviderCostState.Calculated,
                AiProviderCostReason.Calculated, 2.75m));

        await using var context = new ApplicationDbContext(options);
        var service = new AiProviderCostQueryService(context);
        var unfilteredQuery = new AiProviderCostQuery(Utc(0), Utc(24));

        var allCompanies = await service.GetSummaryAsync(unfilteredQuery, CancellationToken.None);
        var oneCompany = await service.GetSummaryAsync(
            unfilteredQuery with { CompanyId = firstCompanyId },
            CancellationToken.None);
        var companyBreakdown = await service.GetBreakdownAsync(
            unfilteredQuery,
            AiProviderCostGroupBy.Company,
            CancellationToken.None);

        Assert.Equal(2, allCompanies.TotalCalls);
        Assert.Equal(2, allCompanies.CalculatedCalls);
        Assert.Equal(4m, allCompanies.CalculatedCostAmount);
        Assert.Equal(1, oneCompany.TotalCalls);
        Assert.Equal(1, oneCompany.CalculatedCalls);
        Assert.Equal(1.25m, oneCompany.CalculatedCostAmount);
        Assert.Equal(2, companyBreakdown.Count);
        Assert.Equal(
            new Dictionary<Guid, decimal>
            {
                [firstCompanyId] = 1.25m,
                [secondCompanyId] = 2.75m
            },
            companyBreakdown.ToDictionary(row => row.CompanyId!.Value, row => row.CalculatedCostAmount));
    }

    [Fact]
    public async Task GetBreakdownAsync_GroupsProviderAndUtcDayAndMonth()
    {
        var (options, companyId) = CreateContextOptions();
        await SeedAsync(options,
            Call(companyId, "OpenAI", "model-a", Utc(1), AiProviderCostState.Calculated,
                AiProviderCostReason.Calculated, 1.25m, OuterOperationType.CopilotOperation),
            Call(companyId, "OpenAI", "model-b", Utc(23), AiProviderCostState.Unknown,
                AiProviderCostReason.UnknownModel, outerOperationType: OuterOperationType.GuestMessage),
            Call(companyId, "Development", null, Utc(25), AiProviderCostState.NotApplicable,
                AiProviderCostReason.NonBillableProvider));

        await using var context = new ApplicationDbContext(options);
        var service = new AiProviderCostQueryService(context);
        var query = new AiProviderCostQuery(Utc(0), Utc(48));

        var providers = await service.GetBreakdownAsync(query, AiProviderCostGroupBy.Provider, CancellationToken.None);
        var models = await service.GetBreakdownAsync(query, AiProviderCostGroupBy.ResolvedModel, CancellationToken.None);
        var companies = await service.GetBreakdownAsync(query, AiProviderCostGroupBy.Company, CancellationToken.None);
        var operations = await service.GetBreakdownAsync(query, AiProviderCostGroupBy.OuterOperationType, CancellationToken.None);
        var days = await service.GetBreakdownAsync(query, AiProviderCostGroupBy.UtcDay, CancellationToken.None);
        var months = await service.GetBreakdownAsync(query, AiProviderCostGroupBy.UtcMonth, CancellationToken.None);

        Assert.Equal(["Development", "OpenAI"], providers.Select(row => row.DimensionKey));
        Assert.Equal(2, providers.Single(row => row.DimensionKey == "OpenAI").TotalCalls);
        Assert.Equal(["Unresolved model", "model-a", "model-b"], models.Select(row => row.DimensionKey));
        Assert.Equal(companyId, Assert.Single(companies).CompanyId);
        Assert.Equal(["CopilotOperation", "GuestMessage", "No outer operation"], operations.Select(row => row.DimensionKey));
        Assert.Equal(2, days.Count);
        Assert.Equal(Utc(0), days[0].BucketStartUtc);
        Assert.Equal(Utc(24), days[1].BucketStartUtc);
        Assert.Equal(1, days[0].CalculatedCalls);
        Assert.Equal(1, days[0].UnknownCalls);
        Assert.Equal(1, days[1].NotApplicableCalls);
        Assert.Single(months);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), months[0].BucketStartUtc);
        Assert.Equal(1.25m, months[0].CalculatedCostAmount);
    }

    [Fact]
    public async Task GetSummaryAsync_RejectsInvalidOrOverlongRanges()
    {
        var (options, _) = CreateContextOptions();
        await using var context = new ApplicationDbContext(options);
        var service = new AiProviderCostQueryService(context);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetSummaryAsync(
            new AiProviderCostQuery(Utc(1), Utc(1)),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetSummaryAsync(
            new AiProviderCostQuery(Utc(0), Utc(367 * 24)),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetSummaryAsync(
            new AiProviderCostQuery(Utc(0).ToOffset(TimeSpan.FromHours(1)), Utc(24)),
            CancellationToken.None));
    }

    private static (DbContextOptions<ApplicationDbContext> Options, Guid CompanyId) CreateContextOptions() =>
        (new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-cost-query-{Guid.NewGuid():N}")
            .Options, Guid.NewGuid());

    private static async Task SeedAsync(
        DbContextOptions<ApplicationDbContext> options,
        params AiProviderCallTelemetry[] calls)
    {
        await using var context = new ApplicationDbContext(options);
        context.AiProviderCallTelemetries.AddRange(calls);
        await context.SaveChangesAsync();
    }

    private static AiProviderCallTelemetry Call(
        Guid companyId,
        string? provider,
        string? resolvedModel,
        DateTimeOffset? dispatchStartedAt,
        AiProviderCostState state,
        AiProviderCostReason reason,
        decimal? amount = null,
        OuterOperationType? outerOperationType = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProviderCallId = Guid.NewGuid().ToString("N"),
            CompanyId = companyId,
            Provider = provider,
            ResolvedModel = resolvedModel,
            OuterOperationType = outerOperationType,
            OuterOperationId = outerOperationType.HasValue ? Guid.NewGuid() : null,
            DispatchStartedAt = dispatchStartedAt,
            LifecycleState = AiProviderCallLifecycleState.Observed,
            CostState = state,
            CostReason = reason,
            PricingVersionId = state == AiProviderCostState.Calculated ? Guid.NewGuid() : null,
            ProviderCostAmount = amount,
            ProviderCostCurrency = amount.HasValue ? "USD" : null,
            CreatedAt = Utc(0),
            UpdatedAt = Utc(0)
        };

    private static DateTimeOffset Utc(int hours) =>
        new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddHours(hours);
}
