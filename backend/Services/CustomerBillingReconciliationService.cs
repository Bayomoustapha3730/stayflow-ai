using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class CustomerBillingReconciliationService(ApplicationDbContext dbContext)
    : ICustomerBillingReconciliationService
{
    public async Task<CustomerBillingSummary> GetSummaryAsync(
        CustomerBillingQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var operations = ApplyFilters(dbContext.CustomerBillableUsageOperations.AsNoTracking(), query);
        var totals = await operations
            .GroupBy(_ => 1)
            .Select(group => new SummaryAggregate(
                group.LongCount(),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Calculated),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Unknown),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.NotBillable),
                group.Sum(item => item.ConsumedQuantity),
                group.Sum(item => item.BillableQuantity),
                group.Sum(item => item.BillingState == CustomerBillableUsageState.Calculated
                    ? item.BillableQuantity
                    : 0L),
                group.Sum(item => item.BillingState == CustomerBillableUsageState.Calculated
                    && item.ChargeAmount != null
                    && item.Currency == "USD"
                    ? item.ChargeAmount.Value
                    : 0m),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Calculated
                    && item.ChargeAmount == 0m
                    && item.Currency == "USD")))
            .SingleOrDefaultAsync(cancellationToken);

        var unknownByReason = await operations
            .Where(item => item.BillingState == CustomerBillableUsageState.Unknown)
            .GroupBy(item => item.BillingReason)
            .Select(group => new CustomerBillingReasonCount(group.Key, group.LongCount()))
            .ToListAsync(cancellationToken);
        var notBillableByReason = await operations
            .Where(item => item.BillingState == CustomerBillableUsageState.NotBillable)
            .GroupBy(item => item.BillingReason)
            .Select(group => new CustomerBillingReasonCount(group.Key, group.LongCount()))
            .ToListAsync(cancellationToken);

        return new CustomerBillingSummary(
            query.FromUtc,
            query.ToUtc,
            totals?.TotalOperations ?? 0,
            totals?.CalculatedOperations ?? 0,
            totals?.UnknownOperations ?? 0,
            totals?.NotBillableOperations ?? 0,
            totals?.TotalConsumedQuantity ?? 0,
            totals?.TotalBillableQuantity ?? 0,
            totals?.CalculatedBillableQuantity ?? 0,
            totals?.CalculatedChargeAmount ?? 0m,
            "USD",
            unknownByReason.OrderBy(item => item.Reason).ToArray(),
            notBillableByReason.OrderBy(item => item.Reason).ToArray(),
            totals?.CalculatedZeroChargeOperations ?? 0);
    }

    public async Task<IReadOnlyList<CustomerBillingBreakdownRow>> GetBreakdownAsync(
        CustomerBillingQuery query,
        CustomerBillingGroupBy groupBy,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        if (!Enum.IsDefined(groupBy))
        {
            throw new ArgumentOutOfRangeException(nameof(groupBy));
        }

        var operations = ApplyFilters(dbContext.CustomerBillableUsageOperations.AsNoTracking(), query);
        return groupBy switch
        {
            CustomerBillingGroupBy.Company => await ByCompanyAsync(operations, cancellationToken),
            CustomerBillingGroupBy.Subscription => await BySubscriptionAsync(operations, cancellationToken),
            CustomerBillingGroupBy.Plan => await ByPlanAsync(operations, cancellationToken),
            CustomerBillingGroupBy.Metric => await ByMetricAsync(operations, cancellationToken),
            CustomerBillingGroupBy.BillingPeriod => await ByBillingPeriodAsync(operations, cancellationToken),
            CustomerBillingGroupBy.BillingState => await ByStateAsync(operations, cancellationToken),
            CustomerBillingGroupBy.BillingReason => await ByReasonAsync(operations, cancellationToken),
            CustomerBillingGroupBy.PricingVersion => await ByPricingVersionAsync(operations, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(groupBy))
        };
    }

    public async Task<CustomerBillingReconciliation> GetReconciliationAsync(
        CustomerBillingQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var source = ApplyFilters(dbContext.CustomerBillableUsageOperations.AsNoTracking(), query);
        var joined =
            from operation in source
            join usage in dbContext.UsageOperations.AsNoTracking()
                on operation.UsageOperationId equals usage.Id into usageGroup
            from usage in usageGroup.DefaultIfEmpty()
            join subscription in dbContext.TenantSubscriptions.AsNoTracking()
                on operation.TenantSubscriptionId equals subscription.Id into subscriptionGroup
            from subscription in subscriptionGroup.DefaultIfEmpty()
            select new ReconciliationFlags(
                usage == null,
                usage != null && usage.CompanyId != operation.CompanyId,
                usage != null && (
                    (operation.Metric == UsageMetric.Users && usage.Metric != "Users")
                    || (operation.Metric == UsageMetric.Properties && usage.Metric != "Properties")
                    || (operation.Metric == UsageMetric.Reservations && usage.Metric != "Reservations")
                    || (operation.Metric == UsageMetric.AiRequests && usage.Metric != "AiRequests")
                    || (operation.Metric == UsageMetric.AiTokens && usage.Metric != "AiTokens")
                    || (operation.Metric == UsageMetric.WhatsAppMessages && usage.Metric != "WhatsAppMessages")
                    || (operation.Metric == UsageMetric.ApiRequests && usage.Metric != "ApiRequests")
                    || (operation.Metric == UsageMetric.StorageBytes && usage.Metric != "StorageBytes")
                    || (operation.Metric == UsageMetric.FileUploads && usage.Metric != "FileUploads")),
                usage != null && usage.PeriodStartUtc != operation.PeriodStartUtc,
                operation.BillingState == CustomerBillableUsageState.Calculated
                    && (operation.BillingReason != CustomerBillableUsageReason.Calculated
                        || operation.ChargeAmount == null
                        || operation.Currency != "USD"
                        || operation.CustomerUsagePriceVersionId == null
                        || operation.UnitQuantity == null
                        || operation.UnitQuantity <= 0
                        || operation.UnitPrice == null
                        || operation.UnitPrice < 0
                        || operation.BillableQuantity <= 0),
                operation.BillingState == CustomerBillableUsageState.NotBillable
                    && (operation.ChargeAmount != null || operation.Currency != null),
                operation.BillingState == CustomerBillableUsageState.Unknown
                    && (operation.ChargeAmount != null || operation.Currency != null),
                operation.UsageAfter - operation.UsageBefore != operation.ConsumedQuantity,
                operation.BillableQuantity > operation.ConsumedQuantity,
                subscription != null && subscription.CompanyId != operation.CompanyId,
                subscription != null && subscription.SubscriptionPlanId != operation.SubscriptionPlanId);

        var aggregate = await joined
            .GroupBy(_ => 1)
            .Select(group => new ReconciliationAggregate(
                group.LongCount(),
                group.LongCount(item => item.MissingUsageOperation),
                group.LongCount(item => item.CompanyMismatch),
                group.LongCount(item => item.MetricMismatch),
                group.LongCount(item => item.PeriodMismatch),
                group.LongCount(item => item.InvalidCalculatedMonetaryFacts),
                group.LongCount(item => item.UnexpectedNotBillableMonetaryFacts),
                group.LongCount(item => item.UnexpectedUnknownMonetaryFacts),
                group.LongCount(item => item.QuantityMismatch),
                group.LongCount(item => item.BillableQuantityExceedsConsumed),
                group.LongCount(item => item.SubscriptionCompanyMismatch),
                group.LongCount(item => item.SubscriptionPlanMismatch)))
            .SingleOrDefaultAsync(cancellationToken);

        var anomalousOperations = await joined
            .Where(item => item.MissingUsageOperation
                || item.CompanyMismatch
                || item.MetricMismatch
                || item.PeriodMismatch
                || item.InvalidCalculatedMonetaryFacts
                || item.UnexpectedNotBillableMonetaryFacts
                || item.UnexpectedUnknownMonetaryFacts
                || item.QuantityMismatch
                || item.BillableQuantityExceedsConsumed
                || item.SubscriptionCompanyMismatch
                || item.SubscriptionPlanMismatch)
            .LongCountAsync(cancellationToken);

        var counts = aggregate is null
            ? new CustomerBillingAnomalyCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
            : new CustomerBillingAnomalyCounts(
                aggregate.MissingUsageOperation,
                aggregate.CompanyMismatch,
                aggregate.MetricMismatch,
                aggregate.PeriodMismatch,
                aggregate.InvalidCalculatedMonetaryFacts,
                aggregate.UnexpectedNotBillableMonetaryFacts,
                aggregate.UnexpectedUnknownMonetaryFacts,
                aggregate.QuantityMismatch,
                aggregate.BillableQuantityExceedsConsumed,
                aggregate.SubscriptionCompanyMismatch,
                aggregate.SubscriptionPlanMismatch);

        return new CustomerBillingReconciliation(
            aggregate?.TotalOperationsExamined ?? 0,
            (aggregate?.TotalOperationsExamined ?? 0) - anomalousOperations,
            anomalousOperations,
            counts);
    }

    internal static void ValidateQuery(CustomerBillingQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.FromUtc.Offset != TimeSpan.Zero || query.ToUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Reporting range timestamps must use UTC.", nameof(query));
        }

        if (query.FromUtc >= query.ToUtc)
        {
            throw new ArgumentException("fromUtc must be earlier than toUtc.", nameof(query));
        }

        if (query.ToUtc - query.FromUtc > TimeSpan.FromDays(366))
        {
            throw new ArgumentException("Reporting windows cannot exceed 366 days.", nameof(query));
        }

        if (query.Metric is { } metric && !Enum.IsDefined(metric))
        {
            throw new ArgumentException("Metric is not supported.", nameof(query));
        }

        if (query.BillingState is { } state && !Enum.IsDefined(state))
        {
            throw new ArgumentException("BillingState is not supported.", nameof(query));
        }

        if (query.BillingReason is { } reason && !Enum.IsDefined(reason))
        {
            throw new ArgumentException("BillingReason is not supported.", nameof(query));
        }
    }

    private static IQueryable<CustomerBillableUsageOperation> ApplyFilters(
        IQueryable<CustomerBillableUsageOperation> operations,
        CustomerBillingQuery query)
    {
        operations = operations
            .Where(item => item.PeriodStartUtc >= query.FromUtc && item.PeriodStartUtc < query.ToUtc);
        if (query.CompanyId is { } companyId)
        {
            operations = operations.Where(item => item.CompanyId == companyId);
        }

        if (query.TenantSubscriptionId is { } subscriptionId)
        {
            operations = operations.Where(item => item.TenantSubscriptionId == subscriptionId);
        }

        if (query.SubscriptionPlanId is { } planId)
        {
            operations = operations.Where(item => item.SubscriptionPlanId == planId);
        }

        if (query.Metric is { } metric)
        {
            operations = operations.Where(item => item.Metric == metric);
        }

        if (query.BillingState is { } state)
        {
            operations = operations.Where(item => item.BillingState == state);
        }

        if (query.BillingReason is { } reason)
        {
            operations = operations.Where(item => item.BillingReason == reason);
        }

        return operations;
    }

    private static async Task<IReadOnlyList<CustomerBillingBreakdownRow>> ByCompanyAsync(
        IQueryable<CustomerBillableUsageOperation> operations,
        CancellationToken cancellationToken)
    {
        var groups = await operations.GroupBy(item => item.CompanyId)
            .Select(group => new BillingAggregate("", group.Key, null, null, null, null, null, null, null, null,
                group.LongCount(), group.LongCount(item => item.BillingState == CustomerBillableUsageState.Calculated),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Unknown),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.NotBillable),
                group.Sum(item => item.ConsumedQuantity), group.Sum(item => item.BillableQuantity),
                group.Sum(item => item.BillingState == CustomerBillableUsageState.Calculated
                    && item.ChargeAmount != null && item.Currency == "USD"
                    ? item.ChargeAmount.Value
                    : 0m))).ToListAsync(cancellationToken);
        return ToRows(groups);
    }

    private static async Task<IReadOnlyList<CustomerBillingBreakdownRow>> BySubscriptionAsync(
        IQueryable<CustomerBillableUsageOperation> operations,
        CancellationToken cancellationToken)
    {
        var groups = await operations.GroupBy(item => item.TenantSubscriptionId)
            .Select(group => new BillingAggregate("", null, group.Key, null, null, null, null, null, null, null,
                group.LongCount(), group.LongCount(item => item.BillingState == CustomerBillableUsageState.Calculated),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Unknown),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.NotBillable),
                group.Sum(item => item.ConsumedQuantity), group.Sum(item => item.BillableQuantity),
                group.Sum(item => item.BillingState == CustomerBillableUsageState.Calculated
                    && item.ChargeAmount != null && item.Currency == "USD"
                    ? item.ChargeAmount.Value
                    : 0m)))
            .ToListAsync(cancellationToken);
        return ToRows(groups);
    }

    private static async Task<IReadOnlyList<CustomerBillingBreakdownRow>> ByPlanAsync(
        IQueryable<CustomerBillableUsageOperation> operations,
        CancellationToken cancellationToken)
    {
        var groups = await operations.GroupBy(item => item.SubscriptionPlanId)
            .Select(group => new BillingAggregate("", null, null, group.Key, null, null, null, null, null, null,
                group.LongCount(), group.LongCount(item => item.BillingState == CustomerBillableUsageState.Calculated),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Unknown),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.NotBillable),
                group.Sum(item => item.ConsumedQuantity), group.Sum(item => item.BillableQuantity),
                group.Sum(item => item.BillingState == CustomerBillableUsageState.Calculated
                    && item.ChargeAmount != null && item.Currency == "USD"
                    ? item.ChargeAmount.Value
                    : 0m)))
            .ToListAsync(cancellationToken);
        return ToRows(groups);
    }

    private static async Task<IReadOnlyList<CustomerBillingBreakdownRow>> ByMetricAsync(
        IQueryable<CustomerBillableUsageOperation> operations,
        CancellationToken cancellationToken)
    {
        var groups = await operations.GroupBy(item => item.Metric)
            .Select(group => new BillingAggregate("", null, null, null, group.Key, null, null, null, null, null,
                group.LongCount(), group.LongCount(item => item.BillingState == CustomerBillableUsageState.Calculated),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Unknown),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.NotBillable),
                group.Sum(item => item.ConsumedQuantity), group.Sum(item => item.BillableQuantity),
                group.Sum(item => item.BillingState == CustomerBillableUsageState.Calculated
                    && item.ChargeAmount != null && item.Currency == "USD"
                    ? item.ChargeAmount.Value
                    : 0m)))
            .ToListAsync(cancellationToken);
        return ToRows(groups);
    }

    private static async Task<IReadOnlyList<CustomerBillingBreakdownRow>> ByBillingPeriodAsync(
        IQueryable<CustomerBillableUsageOperation> operations,
        CancellationToken cancellationToken)
    {
        var groups = await operations.GroupBy(item => new
            {
                item.CompanyId,
                item.TenantSubscriptionId,
                item.SubscriptionPlanId,
                item.PeriodStartUtc,
                item.PeriodEndUtc
            })
            .Select(group => new BillingAggregate(
                "",
                group.Key.CompanyId,
                group.Key.TenantSubscriptionId,
                group.Key.SubscriptionPlanId,
                null,
                group.Key.PeriodStartUtc,
                group.Key.PeriodEndUtc,
                null,
                null,
                null,
                group.LongCount(),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Calculated),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Unknown),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.NotBillable),
                group.Sum(item => item.ConsumedQuantity),
                group.Sum(item => item.BillableQuantity),
                group.Sum(item => item.BillingState == CustomerBillableUsageState.Calculated
                    && item.ChargeAmount != null && item.Currency == "USD"
                    ? item.ChargeAmount.Value
                    : 0m)))
            .ToListAsync(cancellationToken);
        return ToRows(groups);
    }

    private static async Task<IReadOnlyList<CustomerBillingBreakdownRow>> ByStateAsync(
        IQueryable<CustomerBillableUsageOperation> operations,
        CancellationToken cancellationToken)
    {
        var groups = await operations.GroupBy(item => item.BillingState)
            .Select(group => new BillingAggregate("", null, null, null, null, null, null, group.Key, null, null,
                group.LongCount(), group.LongCount(item => item.BillingState == CustomerBillableUsageState.Calculated),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Unknown),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.NotBillable),
                group.Sum(item => item.ConsumedQuantity), group.Sum(item => item.BillableQuantity),
                group.Sum(item => item.BillingState == CustomerBillableUsageState.Calculated
                    && item.ChargeAmount != null && item.Currency == "USD"
                    ? item.ChargeAmount.Value
                    : 0m)))
            .ToListAsync(cancellationToken);
        return ToRows(groups);
    }

    private static async Task<IReadOnlyList<CustomerBillingBreakdownRow>> ByReasonAsync(
        IQueryable<CustomerBillableUsageOperation> operations,
        CancellationToken cancellationToken)
    {
        var groups = await operations.GroupBy(item => item.BillingReason)
            .Select(group => new BillingAggregate("", null, null, null, null, null, null, null, group.Key, null,
                group.LongCount(), group.LongCount(item => item.BillingState == CustomerBillableUsageState.Calculated),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Unknown),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.NotBillable),
                group.Sum(item => item.ConsumedQuantity), group.Sum(item => item.BillableQuantity),
                group.Sum(item => item.BillingState == CustomerBillableUsageState.Calculated
                    && item.ChargeAmount != null && item.Currency == "USD"
                    ? item.ChargeAmount.Value
                    : 0m)))
            .ToListAsync(cancellationToken);
        return ToRows(groups);
    }

    private static async Task<IReadOnlyList<CustomerBillingBreakdownRow>> ByPricingVersionAsync(
        IQueryable<CustomerBillableUsageOperation> operations,
        CancellationToken cancellationToken)
    {
        var groups = await operations.GroupBy(item => item.CustomerUsagePriceVersionId)
            .Select(group => new BillingAggregate("",
                null, null, null, null, null, null, null, null, group.Key,
                group.LongCount(), group.LongCount(item => item.BillingState == CustomerBillableUsageState.Calculated),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.Unknown),
                group.LongCount(item => item.BillingState == CustomerBillableUsageState.NotBillable),
                group.Sum(item => item.ConsumedQuantity), group.Sum(item => item.BillableQuantity),
                group.Sum(item => item.BillingState == CustomerBillableUsageState.Calculated
                    && item.ChargeAmount != null && item.Currency == "USD"
                    ? item.ChargeAmount.Value
                    : 0m)))
            .ToListAsync(cancellationToken);
        return ToRows(groups);
    }

    private static IReadOnlyList<CustomerBillingBreakdownRow> ToRows(IEnumerable<BillingAggregate> groups) =>
        groups.Select(item => item with
            {
                DimensionKey = item.CompanyId?.ToString("D")
                    ?? item.TenantSubscriptionId?.ToString("D")
                    ?? item.SubscriptionPlanId?.ToString("D")
                    ?? item.Metric?.ToString()
                    ?? (item.PeriodStartUtc.HasValue
                        ? $"{item.PeriodStartUtc.Value:O}/{item.PeriodEndUtc:O}"
                        : null)
                    ?? item.BillingState?.ToString()
                    ?? item.BillingReason?.ToString()
                    ?? item.CustomerUsagePriceVersionId?.ToString("D")
                    ?? "No pricing version"
            })
            .OrderBy(item => item.DimensionKey, StringComparer.Ordinal)
            .Select(item => new CustomerBillingBreakdownRow(
                item.DimensionKey,
                item.CompanyId,
                item.TenantSubscriptionId,
                item.SubscriptionPlanId,
                item.Metric,
                item.PeriodStartUtc,
                item.PeriodEndUtc,
                item.BillingState,
                item.BillingReason,
                item.CustomerUsagePriceVersionId,
                item.TotalOperations,
                item.CalculatedOperations,
                item.UnknownOperations,
                item.NotBillableOperations,
                item.ConsumedQuantity,
                item.BillableQuantity,
                item.CalculatedChargeAmount,
                "USD"))
            .ToArray();

    private sealed record SummaryAggregate(
        long TotalOperations,
        long CalculatedOperations,
        long UnknownOperations,
        long NotBillableOperations,
        long TotalConsumedQuantity,
        long TotalBillableQuantity,
        long CalculatedBillableQuantity,
        decimal CalculatedChargeAmount,
        long CalculatedZeroChargeOperations);

    private sealed record BillingAggregate(
        string DimensionKey,
        Guid? CompanyId,
        Guid? TenantSubscriptionId,
        Guid? SubscriptionPlanId,
        UsageMetric? Metric,
        DateTimeOffset? PeriodStartUtc,
        DateTimeOffset? PeriodEndUtc,
        CustomerBillableUsageState? BillingState,
        CustomerBillableUsageReason? BillingReason,
        Guid? CustomerUsagePriceVersionId,
        long TotalOperations,
        long CalculatedOperations,
        long UnknownOperations,
        long NotBillableOperations,
        long ConsumedQuantity,
        long BillableQuantity,
        decimal CalculatedChargeAmount);

    private sealed record ReconciliationFlags(
        bool MissingUsageOperation,
        bool CompanyMismatch,
        bool MetricMismatch,
        bool PeriodMismatch,
        bool InvalidCalculatedMonetaryFacts,
        bool UnexpectedNotBillableMonetaryFacts,
        bool UnexpectedUnknownMonetaryFacts,
        bool QuantityMismatch,
        bool BillableQuantityExceedsConsumed,
        bool SubscriptionCompanyMismatch,
        bool SubscriptionPlanMismatch);

    private sealed record ReconciliationAggregate(
        long TotalOperationsExamined,
        long MissingUsageOperation,
        long CompanyMismatch,
        long MetricMismatch,
        long PeriodMismatch,
        long InvalidCalculatedMonetaryFacts,
        long UnexpectedNotBillableMonetaryFacts,
        long UnexpectedUnknownMonetaryFacts,
        long QuantityMismatch,
        long BillableQuantityExceedsConsumed,
        long SubscriptionCompanyMismatch,
        long SubscriptionPlanMismatch);
}
