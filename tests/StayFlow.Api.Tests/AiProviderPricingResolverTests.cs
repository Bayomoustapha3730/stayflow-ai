using System.Reflection;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class AiProviderPricingResolverTests
{
    [Fact]
    public async Task ResolveAsync_UsesExactProviderAndResolvedModelIdentity()
    {
        var (options, catalog, resolver) = CreateServices();
        await catalog.PublishAsync(Draft("OpenAI", "gpt-exact", EffectiveFrom: At(0)), CancellationToken.None);

        var exact = await resolver.ResolveAsync(" openai ", "gpt-exact", At(1), CancellationToken.None);
        var wrongProvider = await resolver.ResolveAsync("OtherProvider", "gpt-exact", At(1), CancellationToken.None);
        var wrongModel = await resolver.ResolveAsync("OpenAI", "gpt-exact-v2", At(1), CancellationToken.None);
        var prefixModel = await resolver.ResolveAsync("OpenAI", "gpt-exa", At(1), CancellationToken.None);
        var caseChangedModel = await resolver.ResolveAsync("OpenAI", "GPT-exact", At(1), CancellationToken.None);
        var missingResolvedModel = await resolver.ResolveAsync("OpenAI", null, At(1), CancellationToken.None);

        Assert.Equal(AiProviderPricingResolutionState.Resolved, exact.State);
        Assert.Equal(AiProviderPricingResolutionReason.Resolved, exact.Reason);
        Assert.Equal("OPENAI", exact.Provider);
        Assert.Equal("gpt-exact", exact.PricingModelKey);
        Assert.Equal(AiProviderPricingResolutionReason.UnknownProvider, wrongProvider.Reason);
        Assert.Equal(AiProviderPricingResolutionReason.UnknownModel, wrongModel.Reason);
        Assert.Equal(AiProviderPricingResolutionReason.UnknownModel, prefixModel.Reason);
        Assert.Equal(AiProviderPricingResolutionReason.UnknownModel, caseChangedModel.Reason);
        Assert.Equal(AiProviderPricingResolutionReason.MissingResolvedModel, missingResolvedModel.Reason);

        await using var dbContext = new ApplicationDbContext(options);
        var saved = await dbContext.AiProviderPricingVersions.SingleAsync();
        Assert.Equal("OPENAI", saved.Provider);
    }

    [Fact]
    public async Task ResolveAsync_UsesDispatchStartAndHalfOpenEffectiveIntervals()
    {
        var (_, catalog, resolver) = CreateServices();
        var firstId = await catalog.PublishAsync(
            Draft("OpenAI", "sku", EffectiveFrom: At(10), EffectiveTo: At(20), InputPrice: 1m),
            CancellationToken.None);
        var secondId = await catalog.PublishAsync(
            Draft("OpenAI", "sku", EffectiveFrom: At(20), EffectiveTo: At(30), InputPrice: 2m),
            CancellationToken.None);
        var latestId = await catalog.PublishAsync(
            Draft("OpenAI", "sku", EffectiveFrom: At(30), InputPrice: 3m),
            CancellationToken.None);

        var before = await resolver.ResolveAsync("OpenAI", "sku", At(9), CancellationToken.None);
        var start = await resolver.ResolveAsync("OpenAI", "sku", At(10), CancellationToken.None);
        var inside = await resolver.ResolveAsync("OpenAI", "sku", At(19), CancellationToken.None);
        var firstEnd = await resolver.ResolveAsync("OpenAI", "sku", At(20), CancellationToken.None);
        var secondEnd = await resolver.ResolveAsync("OpenAI", "sku", At(30), CancellationToken.None);
        var openEnded = await resolver.ResolveAsync("OpenAI", "sku", At(100), CancellationToken.None);

        Assert.Equal(AiProviderPricingResolutionReason.NoEffectivePrice, before.Reason);
        Assert.Equal(firstId, start.PricingVersionId);
        Assert.Equal(firstId, inside.PricingVersionId);
        Assert.Equal(secondId, firstEnd.PricingVersionId);
        Assert.Equal(latestId, secondEnd.PricingVersionId);
        Assert.Equal(latestId, openEnded.PricingVersionId);
        Assert.Equal(3m, openEnded.InputPrice);
    }

    [Fact]
    public async Task ResolveAsync_RequiresDispatchTimestampAndNeverUsesRequestedModelFallback()
    {
        var (_, catalog, resolver) = CreateServices();
        await catalog.PublishAsync(Draft("OpenAI", "requested-model", EffectiveFrom: At(0)), CancellationToken.None);

        var missingTimestamp = await resolver.ResolveAsync("OpenAI", "requested-model", null, CancellationToken.None);
        var absentResolvedIdentity = await resolver.ResolveAsync("OpenAI", null, At(1), CancellationToken.None);
        var unresolvedModel = await resolver.ResolveAsync("OpenAI", "provider-returned-unmapped-model", At(1), CancellationToken.None);

        Assert.Equal(AiProviderPricingResolutionReason.MissingDispatchTimestamp, missingTimestamp.Reason);
        Assert.Equal(AiProviderPricingResolutionReason.MissingResolvedModel, absentResolvedIdentity.Reason);
        Assert.Equal(AiProviderPricingResolutionState.Unknown, unresolvedModel.State);
        Assert.Equal(AiProviderPricingResolutionReason.UnknownModel, unresolvedModel.Reason);
    }

    [Fact]
    public void ResolveAsync_InputsDoNotIncludeTokenCountsOrRequestedModel()
    {
        var parameters = typeof(IAiProviderPricingResolver)
            .GetMethod(nameof(IAiProviderPricingResolver.ResolveAsync))!
            .GetParameters();

        Assert.Equal(
            ["provider", "resolvedModel", "dispatchStartedAt", "cancellationToken"],
            parameters.Select(parameter => parameter.Name));
        Assert.DoesNotContain(parameters, parameter =>
            parameter.Name is "inputTokens" or "outputTokens" or "totalTokens" or "requestedModel");
    }

    [Fact]
    public async Task ResolveAsync_ReportsUnknownProviderAndModelSeparately()
    {
        var (_, catalog, resolver) = CreateServices();
        await catalog.PublishAsync(Draft("KnownProvider", "known-sku", EffectiveFrom: At(0)), CancellationToken.None);

        var unknownProvider = await resolver.ResolveAsync("OtherProvider", "known-sku", At(1), CancellationToken.None);
        var unknownModel = await resolver.ResolveAsync("KnownProvider", "other-sku", At(1), CancellationToken.None);

        Assert.Equal(AiProviderPricingResolutionReason.UnknownProvider, unknownProvider.Reason);
        Assert.Equal(AiProviderPricingResolutionReason.UnknownModel, unknownModel.Reason);
    }

    [Fact]
    public async Task ResolveAsync_DevelopmentProviderIsNotApplicableWithoutSyntheticPrice()
    {
        var (_, _, resolver) = CreateServices();

        var result = await resolver.ResolveAsync("Development", "stayflow-development-deterministic", null, CancellationToken.None);

        Assert.Equal(AiProviderPricingResolutionState.NotApplicable, result.State);
        Assert.Equal(AiProviderPricingResolutionReason.NonBillableProvider, result.Reason);
        Assert.Null(result.PricingVersionId);
        Assert.Null(result.InputPrice);
        Assert.Null(result.OutputPrice);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsPriceIdentityAndRatesWithoutCalculatingCost()
    {
        var (_, catalog, resolver) = CreateServices();
        var price = 0.123456789012m;
        var versionId = await catalog.PublishAsync(
            Draft("SyntheticProvider", "synthetic-model", EffectiveFrom: At(0),
                InputPrice: price, OutputPrice: 0m, UnitTokenCount: 1_000_000),
            CancellationToken.None);

        var result = await resolver.ResolveAsync("SyntheticProvider", "synthetic-model", At(1), CancellationToken.None);

        Assert.Equal(versionId, result.PricingVersionId);
        Assert.Equal(price, result.InputPrice);
        Assert.Equal(0m, result.OutputPrice);
        Assert.Equal(1_000_000, result.UnitTokenCount);
        Assert.DoesNotContain("Cost", string.Join(',', typeof(AiProviderPricingResolution)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_ResolvesLegitimateZeroInputPrice()
    {
        var (_, catalog, resolver) = CreateServices();
        await catalog.PublishAsync(
            Draft("SyntheticProvider", "zero-input-model", EffectiveFrom: At(0), InputPrice: 0m, OutputPrice: 1.25m),
            CancellationToken.None);

        var result = await resolver.ResolveAsync("SyntheticProvider", "zero-input-model", At(1), CancellationToken.None);

        Assert.Equal(AiProviderPricingResolutionState.Resolved, result.State);
        Assert.Equal(AiProviderPricingResolutionReason.Resolved, result.Reason);
        Assert.Equal(0m, result.InputPrice);
        Assert.Equal(1.25m, result.OutputPrice);
    }

    [Fact]
    public async Task ResolveAsync_FailsClosedWhenMultipleEffectiveVersionsExist()
    {
        var (options, catalog, resolver) = CreateServices();
        await catalog.PublishAsync(
            Draft("SyntheticProvider", "synthetic-model", EffectiveFrom: At(0), EffectiveTo: At(10)),
            CancellationToken.None);
        await using (var dbContext = new ApplicationDbContext(options))
        {
            AddOverlappingVersion(dbContext, Draft(
                "SYNTHETICPROVIDER", "synthetic-model", EffectiveFrom: At(5), EffectiveTo: At(15)));
            await dbContext.SaveChangesAsync();
        }

        var result = await resolver.ResolveAsync("SyntheticProvider", "synthetic-model", At(7), CancellationToken.None);

        Assert.Equal(AiProviderPricingResolutionState.Unknown, result.State);
        Assert.Equal(AiProviderPricingResolutionReason.AmbiguousPrice, result.Reason);
        Assert.Null(result.PricingVersionId);
    }

    [Fact]
    public async Task Catalog_RejectsOverlapAndPublishedVersionsCannotBeUpdated()
    {
        var (options, catalog, _) = CreateServices();
        var id = await catalog.PublishAsync(
            Draft("SyntheticProvider", "sku", EffectiveFrom: At(0), EffectiveTo: At(10)),
            CancellationToken.None);
        await Assert.ThrowsAsync<AiProviderPricingCatalogConflictException>(() => catalog.PublishAsync(
            Draft("SyntheticProvider", "sku", EffectiveFrom: At(9), EffectiveTo: At(20)),
            CancellationToken.None));

        await using var dbContext = new ApplicationDbContext(options);
        var version = await dbContext.AiProviderPricingVersions.SingleAsync(item => item.Id == id);
        dbContext.Entry(version).Property(item => item.InputPrice).CurrentValue = 99m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Catalog_RejectsInvalidMoneyCurrencyIntervalsAndUnits()
    {
        var (_, catalog, _) = CreateServices();

        await Assert.ThrowsAsync<ArgumentException>(() => catalog.PublishAsync(
            Draft(" ", "sku", EffectiveFrom: At(0)),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.PublishAsync(
            Draft("SyntheticProvider", " ", EffectiveFrom: At(0)),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.PublishAsync(
            Draft("SyntheticProvider", " sku ", EffectiveFrom: At(0)),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => catalog.PublishAsync(
            Draft("SyntheticProvider", "sku", EffectiveFrom: At(0), InputPrice: -0.01m),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => catalog.PublishAsync(
            Draft("SyntheticProvider", "sku", EffectiveFrom: At(0), OutputPrice: -0.01m),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.PublishAsync(
            Draft("SyntheticProvider", "sku", EffectiveFrom: At(0), Currency: "EUR"),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.PublishAsync(
            Draft("SyntheticProvider", "sku", EffectiveFrom: At(10), EffectiveTo: At(10)),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => catalog.PublishAsync(
            Draft("SyntheticProvider", "sku", EffectiveFrom: At(0), UnitTokenCount: 0),
            CancellationToken.None));
    }

    private static void AddOverlappingVersion(ApplicationDbContext dbContext, AiProviderPricingVersionDraft draft)
    {
        var entity = (AiProviderPricingVersion)Activator.CreateInstance(typeof(AiProviderPricingVersion), nonPublic: true)!;
        Set(entity, nameof(AiProviderPricingVersion.Provider), draft.Provider);
        Set(entity, nameof(AiProviderPricingVersion.PricingModelKey), draft.PricingModelKey);
        Set(entity, nameof(AiProviderPricingVersion.EffectiveFrom), draft.EffectiveFrom);
        Set(entity, nameof(AiProviderPricingVersion.EffectiveTo), draft.EffectiveTo);
        Set(entity, nameof(AiProviderPricingVersion.Currency), draft.Currency);
        Set(entity, nameof(AiProviderPricingVersion.InputPrice), draft.InputPrice);
        Set(entity, nameof(AiProviderPricingVersion.OutputPrice), draft.OutputPrice);
        Set(entity, nameof(AiProviderPricingVersion.UnitTokenCount), draft.UnitTokenCount);
        dbContext.AiProviderPricingVersions.Add(entity);
    }

    private static void Set(AiProviderPricingVersion version, string propertyName, object? value)
    {
        typeof(AiProviderPricingVersion)
            .GetProperty(propertyName)!
            .SetValue(version, value);
    }

    private static AiProviderPricingVersionDraft Draft(
        string provider,
        string model,
        DateTimeOffset EffectiveFrom,
        DateTimeOffset? EffectiveTo = null,
        string Currency = "USD",
        decimal InputPrice = 1.25m,
        decimal OutputPrice = 2.5m,
        int UnitTokenCount = 1_000_000) =>
        new(provider, model, EffectiveFrom, EffectiveTo, Currency, InputPrice, OutputPrice, UnitTokenCount);

    private static DateTimeOffset At(int day) =>
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day);

    private static (DbContextOptions<ApplicationDbContext> Options, AiProviderPricingCatalog Catalog, AiProviderPricingResolver Resolver)
        CreateServices()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-provider-pricing-{Guid.NewGuid():N}")
            .Options;
        return (
            options,
            new AiProviderPricingCatalog(options),
            new AiProviderPricingResolver(options));
    }
}
