using System.Data;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class CustomerUsagePricingCatalog(
    DbContextOptions<ApplicationDbContext> dbContextOptions) : ICustomerUsagePricingCatalog
{
    public async Task<Guid> PublishAsync(CustomerUsagePriceVersionDraft draft, CancellationToken cancellationToken)
    {
        Validate(draft);
        var normalizedDraft = draft with { Currency = draft.Currency.Trim().ToUpperInvariant() };

        await using var dbContext = new ApplicationDbContext(dbContextOptions);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var overlapsExisting = await dbContext.CustomerUsagePriceVersions
            .AnyAsync(version =>
                version.SubscriptionPlanId == normalizedDraft.SubscriptionPlanId
                && version.Metric == normalizedDraft.Metric
                && (normalizedDraft.EffectiveToUtc == null
                    || version.EffectiveFromUtc < normalizedDraft.EffectiveToUtc)
                && (version.EffectiveToUtc == null
                    || version.EffectiveToUtc > normalizedDraft.EffectiveFromUtc),
                cancellationToken);
        if (overlapsExisting)
        {
            throw new CustomerUsagePricingCatalogConflictException(
                "An effective customer usage price already exists for this plan and metric.");
        }

        var version = CustomerUsagePriceVersion.Create(normalizedDraft);
        dbContext.CustomerUsagePriceVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return version.Id;
    }

    private static void Validate(CustomerUsagePriceVersionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.SubscriptionPlanId == Guid.Empty)
        {
            throw new ArgumentException("SubscriptionPlanId must be present.", nameof(draft));
        }

        if (!Enum.IsDefined(draft.Metric))
        {
            throw new ArgumentException("Metric must be a supported usage metric.", nameof(draft));
        }

        if (draft.BillingMode is not (CustomerUsageBillingMode.IncludedOnly
            or CustomerUsageBillingMode.PerUnitOverage
            or CustomerUsageBillingMode.NotBillable))
        {
            throw new ArgumentException("BillingMode must be supported.", nameof(draft));
        }

        if (draft.EffectiveToUtc <= draft.EffectiveFromUtc)
        {
            throw new ArgumentException("EffectiveToUtc must be later than EffectiveFromUtc when supplied.", nameof(draft));
        }

        if (!string.Equals(draft.Currency?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only USD pricing is supported.", nameof(draft));
        }

        if (draft.UnitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(draft), "UnitPrice cannot be negative.");
        }

        if (draft.UnitQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(draft), "UnitQuantity must be greater than zero.");
        }
    }
}
