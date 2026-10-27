using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class AiProviderCostCalculatorTests
{
    [Fact]
    public async Task CalculateAsync_UsesExactPricingVersionAndInputOutputDimensions()
    {
        var (catalog, calculator) = CreateCalculator();
        var versionId = await catalog.PublishAsync(Draft(inputPrice: 2.5m, outputPrice: 5m), CancellationToken.None);

        var result = await calculator.CalculateAsync(
            Facts(inputTokens: 2_000, outputTokens: 1_000),
            CancellationToken.None);

        Assert.Equal(AiProviderCostState.Calculated, result.State);
        Assert.Equal(AiProviderCostReason.Calculated, result.Reason);
        Assert.Equal(versionId, result.PricingVersionId);
        Assert.Equal(0.01m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public async Task CalculateAsync_SupportsInputOnlyOutputOnlyAndKnownZeroUsage()
    {
        var (catalog, calculator) = CreateCalculator();
        await catalog.PublishAsync(Draft(inputPrice: 2m, outputPrice: 4m), CancellationToken.None);

        var inputOnly = await calculator.CalculateAsync(Facts(1_000, 0), CancellationToken.None);
        var outputOnly = await calculator.CalculateAsync(Facts(0, 1_000), CancellationToken.None);
        var zero = await calculator.CalculateAsync(Facts(0, 0), CancellationToken.None);

        Assert.Equal(0.002m, inputOnly.Amount);
        Assert.Equal(0.004m, outputOnly.Amount);
        Assert.Equal(AiProviderCostState.Calculated, zero.State);
        Assert.Equal(0m, zero.Amount);
    }

    [Fact]
    public async Task CalculateAsync_PreservesSubCentPrecisionWithoutCentRounding()
    {
        var (catalog, calculator) = CreateCalculator();
        await catalog.PublishAsync(Draft(inputPrice: 0.123456789012m, outputPrice: 0m), CancellationToken.None);

        var result = await calculator.CalculateAsync(Facts(1, 0), CancellationToken.None);

        Assert.Equal(0.000000123456789012m, result.Amount);
        Assert.NotEqual(0m, result.Amount);
    }

    [Fact]
    public async Task CalculateAsync_UnknownUsageNeverBecomesZero()
    {
        var (catalog, calculator) = CreateCalculator();
        await catalog.PublishAsync(Draft(), CancellationToken.None);

        var result = await calculator.CalculateAsync(Facts(null, null), CancellationToken.None);

        Assert.Equal(AiProviderCostState.Unknown, result.State);
        Assert.Equal(AiProviderCostReason.MissingTokenUsage, result.Reason);
        Assert.Null(result.Amount);
        Assert.Null(result.Currency);
    }

    [Fact]
    public async Task CalculateAsync_MapsPricingResolutionFailures()
    {
        var (catalog, calculator) = CreateCalculator();
        await catalog.PublishAsync(
            Draft() with
            {
                PricingModelKey = "future-model",
                EffectiveFrom = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            CancellationToken.None);

        var unknownProvider = await calculator.CalculateAsync(
            Facts(1, 1, provider: "unpriced-provider"), CancellationToken.None);
        var missingModel = await calculator.CalculateAsync(
            Facts(1, 1, resolvedModel: null), CancellationToken.None);
        var missingTimestamp = await calculator.CalculateAsync(
            new AiProviderCostFacts("SyntheticProvider", "synthetic-model", null, 1, 1),
            CancellationToken.None);
        var unknownModel = await calculator.CalculateAsync(
            Facts(1, 1, resolvedModel: "unmapped-model"), CancellationToken.None);
        var noEffectivePrice = await calculator.CalculateAsync(
            Facts(1, 1, resolvedModel: "future-model"), CancellationToken.None);

        Assert.Equal(AiProviderCostReason.UnknownProvider, unknownProvider.Reason);
        Assert.Equal(AiProviderCostReason.MissingResolvedModel, missingModel.Reason);
        Assert.Equal(AiProviderCostReason.MissingDispatchTimestamp, missingTimestamp.Reason);
        Assert.Equal(AiProviderCostReason.UnknownModel, unknownModel.Reason);
        Assert.Equal(AiProviderCostReason.NoEffectivePrice, noEffectivePrice.Reason);
        Assert.All([unknownProvider, missingModel, missingTimestamp, unknownModel, noEffectivePrice], result =>
        {
            Assert.Equal(AiProviderCostState.Unknown, result.State);
            Assert.Null(result.Amount);
        });
    }

    [Fact]
    public async Task CalculateAsync_MapsAmbiguousAndUnsupportedPricingResults()
    {
        var ambiguousCalculator = new AiProviderCostCalculator(new FixedPricingResolver(
            new AiProviderPricingResolution(
                AiProviderPricingResolutionState.Unknown,
                AiProviderPricingResolutionReason.AmbiguousPrice)));
        var unsupportedCalculator = new AiProviderCostCalculator(new FixedPricingResolver(
            new AiProviderPricingResolution(
                AiProviderPricingResolutionState.Resolved,
                AiProviderPricingResolutionReason.Resolved,
                Guid.NewGuid(),
                Currency: "USD",
                InputPrice: 1m,
                OutputPrice: 1m)));

        var ambiguous = await ambiguousCalculator.CalculateAsync(Facts(1, 1), CancellationToken.None);
        var unsupported = await unsupportedCalculator.CalculateAsync(Facts(1, 1), CancellationToken.None);

        Assert.Equal(AiProviderCostReason.AmbiguousPrice, ambiguous.Reason);
        Assert.Equal(AiProviderCostState.Unknown, ambiguous.State);
        Assert.Equal(AiProviderCostReason.UnsupportedPricingDimension, unsupported.Reason);
        Assert.Equal(AiProviderCostState.Unknown, unsupported.State);
        Assert.NotNull(unsupported.PricingVersionId);
    }

    [Fact]
    public async Task CalculateAsync_DevelopmentAndUnavailableProviderUsageHaveDistinctStates()
    {
        var (_, calculator) = CreateCalculator();

        var development = await calculator.CalculateAsync(
            Facts(null, null, provider: "Development", resolvedModel: null, dispatchStartedAt: null),
            CancellationToken.None);
        var unavailable = await calculator.CalculateAsync(
            Facts(null, null, providerUsageUnavailable: true),
            CancellationToken.None);

        Assert.Equal(AiProviderCostState.NotApplicable, development.State);
        Assert.Equal(AiProviderCostReason.NonBillableProvider, development.Reason);
        Assert.Null(development.Amount);
        Assert.Equal(AiProviderCostState.Unknown, unavailable.State);
        Assert.Equal(AiProviderCostReason.ProviderUsageUnavailable, unavailable.Reason);
        Assert.Null(unavailable.Amount);
    }

    [Fact]
    public async Task CalculateAsync_RejectsNegativeTokenFacts()
    {
        var (_, calculator) = CreateCalculator();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            calculator.CalculateAsync(Facts(-1, 0), CancellationToken.None));
    }

    [Fact]
    public void CostFacts_DoNotAcceptProviderReportedTotalTokens()
    {
        var properties = typeof(AiProviderCostFacts).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain("TotalTokens", properties);
    }

    private static AiProviderCostFacts Facts(
        long? inputTokens,
        long? outputTokens,
        string? provider = "SyntheticProvider",
        string? resolvedModel = "synthetic-model",
        DateTimeOffset? dispatchStartedAt = null,
        bool providerUsageUnavailable = false) =>
        new(
            provider,
            resolvedModel,
            dispatchStartedAt ?? new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            inputTokens,
            outputTokens,
            providerUsageUnavailable);

    private static AiProviderPricingVersionDraft Draft(decimal inputPrice = 1m, decimal outputPrice = 1m) =>
        new(
            "SyntheticProvider",
            "synthetic-model",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            null,
            "USD",
            inputPrice,
            outputPrice,
            1_000_000);

    private static (AiProviderPricingCatalog Catalog, AiProviderCostCalculator Calculator) CreateCalculator()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-provider-cost-{Guid.NewGuid():N}")
            .Options;
        var catalog = new AiProviderPricingCatalog(options);
        return (catalog, new AiProviderCostCalculator(new AiProviderPricingResolver(options)));
    }

    private sealed class FixedPricingResolver(AiProviderPricingResolution result) : IAiProviderPricingResolver
    {
        public Task<AiProviderPricingResolution> ResolveAsync(
            string? provider,
            string? resolvedModel,
            DateTimeOffset? dispatchStartedAt,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
