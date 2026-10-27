using System.Data;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class AiProviderPricingCatalog(
    DbContextOptions<ApplicationDbContext> dbContextOptions) : IAiProviderPricingCatalog
{
    public async Task<Guid> PublishAsync(AiProviderPricingVersionDraft draft, CancellationToken cancellationToken)
    {
        Validate(draft);
        var provider = AiProviderPricingIdentity.NormalizeProvider(draft.Provider);
        if (provider.Length is 0 or > 80)
        {
            throw new ArgumentException("Normalized provider must be no longer than 80 characters.", nameof(draft));
        }
        var normalizedDraft = draft with
        {
            Provider = provider,
            PricingModelKey = draft.PricingModelKey,
            Currency = draft.Currency.Trim().ToUpperInvariant()
        };

        await using var dbContext = new ApplicationDbContext(dbContextOptions);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var overlapsExisting = await dbContext.AiProviderPricingVersions
            .AnyAsync(version =>
                version.Provider == normalizedDraft.Provider
                && version.PricingModelKey == normalizedDraft.PricingModelKey
                && (normalizedDraft.EffectiveTo == null || version.EffectiveFrom < normalizedDraft.EffectiveTo)
                && (version.EffectiveTo == null || version.EffectiveTo > normalizedDraft.EffectiveFrom),
                cancellationToken);
        if (overlapsExisting)
        {
            throw new AiProviderPricingCatalogConflictException(
                "An effective pricing version already exists for this provider and pricing model key.");
        }

        var version = AiProviderPricingVersion.Create(normalizedDraft);
        dbContext.AiProviderPricingVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return version.Id;
    }

    private static void Validate(AiProviderPricingVersionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrWhiteSpace(draft.Provider) || draft.Provider.Length > 80)
        {
            throw new ArgumentException("Provider must be present and no longer than 80 characters.", nameof(draft));
        }

        if (string.IsNullOrWhiteSpace(draft.PricingModelKey)
            || draft.PricingModelKey.Length > 120
            || !string.Equals(draft.PricingModelKey, draft.PricingModelKey.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Pricing model key must be present, bounded, and have no surrounding whitespace.", nameof(draft));
        }

        if (draft.EffectiveTo <= draft.EffectiveFrom)
        {
            throw new ArgumentException("EffectiveTo must be later than EffectiveFrom when supplied.", nameof(draft));
        }

        if (!string.Equals(draft.Currency?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only USD pricing is supported.", nameof(draft));
        }

        if (draft.InputPrice < 0 || draft.OutputPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(draft), "Pricing amounts cannot be negative.");
        }

        if (draft.UnitTokenCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(draft), "UnitTokenCount must be greater than zero.");
        }
    }
}
