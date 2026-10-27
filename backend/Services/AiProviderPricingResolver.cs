using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;

namespace StayFlow.Api.Services;

public sealed class AiProviderPricingResolver(
    DbContextOptions<ApplicationDbContext> dbContextOptions) : IAiProviderPricingResolver
{
    public async Task<AiProviderPricingResolution> ResolveAsync(
        string? provider,
        string? resolvedModel,
        DateTimeOffset? dispatchStartedAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return Unknown(AiProviderPricingResolutionReason.UnknownProvider);
        }

        var normalizedProvider = AiProviderPricingIdentity.NormalizeProvider(provider);
        if (normalizedProvider == "DEVELOPMENT")
        {
            return new AiProviderPricingResolution(
                AiProviderPricingResolutionState.NotApplicable,
                AiProviderPricingResolutionReason.NonBillableProvider);
        }

        if (string.IsNullOrWhiteSpace(resolvedModel))
        {
            return Unknown(AiProviderPricingResolutionReason.MissingResolvedModel);
        }

        if (dispatchStartedAt is null)
        {
            return Unknown(AiProviderPricingResolutionReason.MissingDispatchTimestamp);
        }

        await using var dbContext = new ApplicationDbContext(dbContextOptions);
        var providerExists = await dbContext.AiProviderPricingVersions
            .AnyAsync(version => version.Provider == normalizedProvider, cancellationToken);
        if (!providerExists)
        {
            return Unknown(AiProviderPricingResolutionReason.UnknownProvider);
        }

        var modelExists = await dbContext.AiProviderPricingVersions
            .AnyAsync(version => version.Provider == normalizedProvider
                && version.PricingModelKey == resolvedModel, cancellationToken);
        if (!modelExists)
        {
            return Unknown(AiProviderPricingResolutionReason.UnknownModel);
        }

        var matchingVersions = await dbContext.AiProviderPricingVersions
            .AsNoTracking()
            .Where(version => version.Provider == normalizedProvider
                && version.PricingModelKey == resolvedModel
                && version.EffectiveFrom <= dispatchStartedAt.Value
                && (version.EffectiveTo == null || dispatchStartedAt.Value < version.EffectiveTo))
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matchingVersions.Count > 1)
        {
            return Unknown(AiProviderPricingResolutionReason.AmbiguousPrice);
        }

        if (matchingVersions.Count == 0)
        {
            return Unknown(AiProviderPricingResolutionReason.NoEffectivePrice);
        }

        var match = matchingVersions[0];
        return new AiProviderPricingResolution(
            AiProviderPricingResolutionState.Resolved,
            AiProviderPricingResolutionReason.Resolved,
            match.Id,
            match.Provider,
            match.PricingModelKey,
            match.EffectiveFrom,
            match.EffectiveTo,
            match.Currency,
            match.InputPrice,
            match.OutputPrice,
            match.UnitTokenCount);
    }

    private static AiProviderPricingResolution Unknown(AiProviderPricingResolutionReason reason) =>
        new(AiProviderPricingResolutionState.Unknown, reason);
}
