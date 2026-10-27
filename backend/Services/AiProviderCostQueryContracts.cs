namespace StayFlow.Api.Services;

public enum AiProviderCostGroupBy
{
    Provider,
    ResolvedModel,
    Company,
    UtcDay,
    UtcMonth,
    OuterOperationType
}

public sealed record AiProviderCostQuery(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    Guid? CompanyId = null,
    string? Provider = null,
    string? ResolvedModel = null);

public sealed record AiProviderCostUnknownReasonCount(AiProviderCostReason Reason, long Calls);

public sealed record AiProviderCostSummary(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    long TotalCalls,
    long CalculatedCalls,
    long UnknownCalls,
    long NotApplicableCalls,
    long MissingDispatchTimestampCalls,
    decimal CalculatedCostAmount,
    string Currency,
    IReadOnlyList<AiProviderCostUnknownReasonCount> UnknownByReason);

public sealed record AiProviderCostBreakdownRow(
    string DimensionKey,
    Guid? CompanyId,
    DateTimeOffset? BucketStartUtc,
    long TotalCalls,
    long CalculatedCalls,
    long UnknownCalls,
    long NotApplicableCalls,
    decimal CalculatedCostAmount,
    string Currency);

public interface IAiProviderCostQueryService
{
    Task<AiProviderCostSummary> GetSummaryAsync(AiProviderCostQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<AiProviderCostBreakdownRow>> GetBreakdownAsync(
        AiProviderCostQuery query,
        AiProviderCostGroupBy groupBy,
        CancellationToken cancellationToken);
}
