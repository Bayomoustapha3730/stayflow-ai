namespace StayFlow.Api.Services;

public sealed record AiProviderPricingVersionDraft(
    string Provider,
    string PricingModelKey,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Currency,
    decimal InputPrice,
    decimal OutputPrice,
    int UnitTokenCount);

public enum AiProviderPricingResolutionState
{
    Resolved,
    Unknown,
    NotApplicable
}

public enum AiProviderPricingResolutionReason
{
    Resolved,
    MissingResolvedModel,
    UnknownProvider,
    UnknownModel,
    NoEffectivePrice,
    AmbiguousPrice,
    MissingDispatchTimestamp,
    NonBillableProvider,
    // Reserved for Phase 2C.3 cost calculation, which evaluates observable pricing dimensions.
    UnsupportedPricingDimension
}

public sealed record AiProviderPricingResolution(
    AiProviderPricingResolutionState State,
    AiProviderPricingResolutionReason Reason,
    Guid? PricingVersionId = null,
    string? Provider = null,
    string? PricingModelKey = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null,
    string? Currency = null,
    decimal? InputPrice = null,
    decimal? OutputPrice = null,
    int? UnitTokenCount = null);

public interface IAiProviderPricingResolver
{
    Task<AiProviderPricingResolution> ResolveAsync(
        string? provider,
        string? resolvedModel,
        DateTimeOffset? dispatchStartedAt,
        CancellationToken cancellationToken);
}

public interface IAiProviderPricingCatalog
{
    Task<Guid> PublishAsync(AiProviderPricingVersionDraft draft, CancellationToken cancellationToken);
}

public sealed class AiProviderPricingCatalogConflictException(string message) : InvalidOperationException(message);
