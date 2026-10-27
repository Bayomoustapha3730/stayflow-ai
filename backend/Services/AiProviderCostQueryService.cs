using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class AiProviderCostQueryService(ApplicationDbContext dbContext) : IAiProviderCostQueryService
{
    public async Task<AiProviderCostSummary> GetSummaryAsync(
        AiProviderCostQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var all = ApplyIdentityFilters(dbContext.AiProviderCallTelemetries.AsNoTracking(), query);
        var missingDispatchTimestampCalls = await all.LongCountAsync(call => call.DispatchStartedAt == null, cancellationToken);
        var calls = ApplyTimeRange(all, query);
        var totalCalls = await calls.LongCountAsync(cancellationToken);
        var calculatedCalls = await calls.LongCountAsync(call => call.CostState == AiProviderCostState.Calculated, cancellationToken);
        var unknownCalls = await calls.LongCountAsync(call => call.CostState == AiProviderCostState.Unknown, cancellationToken);
        var notApplicableCalls = await calls.LongCountAsync(call => call.CostState == AiProviderCostState.NotApplicable, cancellationToken);
        var calculatedCost = await calls.SumAsync(
            call => call.CostState == AiProviderCostState.Calculated
                && call.ProviderCostCurrency == "USD"
                ? call.ProviderCostAmount ?? 0m
                : 0m,
            cancellationToken);
        var unknownByReason = await calls
            .Where(call => call.CostState == AiProviderCostState.Unknown)
            .GroupBy(call => call.CostReason)
            .Select(group => new { Reason = group.Key, Calls = group.LongCount() })
            .OrderBy(item => item.Reason)
            .ToListAsync(cancellationToken);
        var unknownReasonCounts = unknownByReason
            .Select(item => new AiProviderCostUnknownReasonCount(item.Reason, item.Calls))
            .ToArray();

        return new AiProviderCostSummary(
            query.FromUtc.ToUniversalTime(),
            query.ToUtc.ToUniversalTime(),
            totalCalls,
            calculatedCalls,
            unknownCalls,
            notApplicableCalls,
            missingDispatchTimestampCalls,
            calculatedCost,
            "USD",
            unknownReasonCounts);
    }

    public async Task<IReadOnlyList<AiProviderCostBreakdownRow>> GetBreakdownAsync(
        AiProviderCostQuery query,
        AiProviderCostGroupBy groupBy,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        if (!Enum.IsDefined(groupBy))
        {
            throw new ArgumentOutOfRangeException(nameof(groupBy));
        }

        var calls = ApplyTimeRange(
            ApplyIdentityFilters(dbContext.AiProviderCallTelemetries.AsNoTracking(), query),
            query);

        return groupBy switch
        {
            AiProviderCostGroupBy.Provider => await GroupByStringAsync(calls, call => call.Provider, "Unknown provider", cancellationToken),
            AiProviderCostGroupBy.ResolvedModel => await GroupByStringAsync(calls, call => call.ResolvedModel, "Unresolved model", cancellationToken),
            AiProviderCostGroupBy.Company => await GroupByCompanyAsync(calls, cancellationToken),
            AiProviderCostGroupBy.UtcDay => await GroupByTimeAsync(calls, "day", cancellationToken),
            AiProviderCostGroupBy.UtcMonth => await GroupByTimeAsync(calls, "month", cancellationToken),
            AiProviderCostGroupBy.OuterOperationType => await GroupByStringAsync(
                calls,
                call => call.OuterOperationType.HasValue ? call.OuterOperationType.Value.ToString() : null,
                "No outer operation",
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(groupBy))
        };
    }

    private static async Task<IReadOnlyList<AiProviderCostBreakdownRow>> GroupByStringAsync(
        IQueryable<AiProviderCallTelemetry> calls,
        Expression<Func<AiProviderCallTelemetry, string?>> keySelector,
        string nullKey,
        CancellationToken cancellationToken)
    {
        var groups = await calls
            .GroupBy(keySelector)
            .Select(group => new StringGroupAggregate(
                group.Key,
                group.LongCount(),
                group.LongCount(call => call.CostState == AiProviderCostState.Calculated),
                group.LongCount(call => call.CostState == AiProviderCostState.Unknown),
                group.LongCount(call => call.CostState == AiProviderCostState.NotApplicable),
                group.Sum(call => call.CostState == AiProviderCostState.Calculated
                    && call.ProviderCostCurrency == "USD"
                    ? call.ProviderCostAmount ?? 0m
                    : 0m)))
            .ToListAsync(cancellationToken);

        return groups.Select(group => ToBreakdown(group.Key ?? nullKey, null, null, group))
            .OrderBy(row => row.DimensionKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<IReadOnlyList<AiProviderCostBreakdownRow>> GroupByCompanyAsync(
        IQueryable<AiProviderCallTelemetry> calls,
        CancellationToken cancellationToken)
    {
        var groups = await calls
            .GroupBy(call => call.CompanyId)
            .Select(group => new CompanyGroupAggregate(
                group.Key,
                group.LongCount(),
                group.LongCount(call => call.CostState == AiProviderCostState.Calculated),
                group.LongCount(call => call.CostState == AiProviderCostState.Unknown),
                group.LongCount(call => call.CostState == AiProviderCostState.NotApplicable),
                group.Sum(call => call.CostState == AiProviderCostState.Calculated
                    && call.ProviderCostCurrency == "USD"
                    ? call.ProviderCostAmount ?? 0m
                    : 0m)))
            .ToListAsync(cancellationToken);

        return groups.Select(group => ToBreakdown(group.CompanyId.ToString("D"), group.CompanyId, null, group))
            .OrderBy(row => row.CompanyId)
            .ToArray();
    }

    private static async Task<IReadOnlyList<AiProviderCostBreakdownRow>> GroupByTimeAsync(
        IQueryable<AiProviderCallTelemetry> calls,
        string precision,
        CancellationToken cancellationToken)
    {
        var groups = await calls
            .GroupBy(call => UtcDateTrunc(precision, call.DispatchStartedAt!.Value, "UTC"))
            .Select(group => new TimeGroupAggregate(
                group.Key,
                group.LongCount(),
                group.LongCount(call => call.CostState == AiProviderCostState.Calculated),
                group.LongCount(call => call.CostState == AiProviderCostState.Unknown),
                group.LongCount(call => call.CostState == AiProviderCostState.NotApplicable),
                group.Sum(call => call.CostState == AiProviderCostState.Calculated
                    && call.ProviderCostCurrency == "USD"
                    ? call.ProviderCostAmount ?? 0m
                    : 0m)))
            .ToListAsync(cancellationToken);

        return groups.Select(group => ToBreakdown(group.BucketStartUtc.ToString("O"), null, group.BucketStartUtc, group))
            .OrderBy(row => row.BucketStartUtc)
            .ToArray();
    }

    [DbFunction("date_trunc", IsBuiltIn = true)]
    public static DateTimeOffset UtcDateTrunc(string precision, DateTimeOffset timestamp, string timeZone)
    {
        var utc = timestamp.ToUniversalTime();
        return precision switch
        {
            "day" => new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero),
            "month" => new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero),
            _ => throw new ArgumentOutOfRangeException(nameof(precision))
        };
    }

    private static IQueryable<AiProviderCallTelemetry> ApplyIdentityFilters(
        IQueryable<AiProviderCallTelemetry> calls,
        AiProviderCostQuery query)
    {
        if (query.CompanyId.HasValue)
        {
            calls = calls.Where(call => call.CompanyId == query.CompanyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Provider))
        {
            calls = calls.Where(call => call.Provider == query.Provider);
        }

        if (!string.IsNullOrWhiteSpace(query.ResolvedModel))
        {
            calls = calls.Where(call => call.ResolvedModel == query.ResolvedModel);
        }

        return calls;
    }

    private static IQueryable<AiProviderCallTelemetry> ApplyTimeRange(
        IQueryable<AiProviderCallTelemetry> calls,
        AiProviderCostQuery query)
    {
        var fromUtc = query.FromUtc.ToUniversalTime();
        var toUtc = query.ToUtc.ToUniversalTime();
        return calls.Where(call =>
            call.DispatchStartedAt.HasValue
            && call.DispatchStartedAt.Value >= fromUtc
            && call.DispatchStartedAt.Value < toUtc);
    }

    private static void ValidateQuery(AiProviderCostQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.FromUtc.Offset != TimeSpan.Zero || query.ToUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Reporting boundaries must be expressed in UTC.", nameof(query));
        }

        if (query.FromUtc >= query.ToUtc)
        {
            throw new ArgumentException("fromUtc must be earlier than toUtc.", nameof(query));
        }

        if (query.ToUtc - query.FromUtc > TimeSpan.FromDays(366))
        {
            throw new ArgumentException("Reporting windows cannot exceed 366 days.", nameof(query));
        }
    }

    private static AiProviderCostBreakdownRow ToBreakdown(
        string key,
        Guid? companyId,
        DateTimeOffset? bucketStartUtc,
        GroupAggregate group) =>
        new(
            key,
            companyId,
            bucketStartUtc,
            group.TotalCalls,
            group.CalculatedCalls,
            group.UnknownCalls,
            group.NotApplicableCalls,
            group.CalculatedCostAmount,
            "USD");

    private abstract record GroupAggregate(
        long TotalCalls,
        long CalculatedCalls,
        long UnknownCalls,
        long NotApplicableCalls,
        decimal CalculatedCostAmount);

    private sealed record StringGroupAggregate(
        string? Key,
        long TotalCalls,
        long CalculatedCalls,
        long UnknownCalls,
        long NotApplicableCalls,
        decimal CalculatedCostAmount)
        : GroupAggregate(TotalCalls, CalculatedCalls, UnknownCalls, NotApplicableCalls, CalculatedCostAmount);

    private sealed record CompanyGroupAggregate(
        Guid CompanyId,
        long TotalCalls,
        long CalculatedCalls,
        long UnknownCalls,
        long NotApplicableCalls,
        decimal CalculatedCostAmount)
        : GroupAggregate(TotalCalls, CalculatedCalls, UnknownCalls, NotApplicableCalls, CalculatedCostAmount);

    private sealed record TimeGroupAggregate(
        DateTimeOffset BucketStartUtc,
        long TotalCalls,
        long CalculatedCalls,
        long UnknownCalls,
        long NotApplicableCalls,
        decimal CalculatedCostAmount)
        : GroupAggregate(TotalCalls, CalculatedCalls, UnknownCalls, NotApplicableCalls, CalculatedCostAmount);
}
