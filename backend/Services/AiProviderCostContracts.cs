namespace StayFlow.Api.Services;

public enum AiProviderCostState
{
    Unknown,
    Calculated,
    NotApplicable
}

public enum AiProviderCostReason
{
    Calculated,
    MissingResolvedModel,
    MissingDispatchTimestamp,
    UnknownProvider,
    UnknownModel,
    NoEffectivePrice,
    AmbiguousPrice,
    MissingTokenUsage,
    UnsupportedPricingDimension,
    ProviderUsageUnavailable,
    NonBillableProvider,
    CalculationOverflow
}

public sealed record AiProviderCostFacts(
    string? Provider,
    string? ResolvedModel,
    DateTimeOffset? DispatchStartedAt,
    long? InputTokens,
    long? OutputTokens,
    bool ProviderUsageUnavailable = false);

public sealed record AiProviderCostResult(
    AiProviderCostState State,
    AiProviderCostReason Reason,
    Guid? PricingVersionId = null,
    decimal? Amount = null,
    string? Currency = null);

public interface IAiProviderCostCalculator
{
    Task<AiProviderCostResult> CalculateAsync(AiProviderCostFacts facts, CancellationToken cancellationToken);
}
