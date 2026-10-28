using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public enum CustomerBillingGroupBy
{
    Company,
    Subscription,
    Plan,
    Metric,
    BillingPeriod,
    BillingState,
    BillingReason,
    PricingVersion
}

public sealed record CustomerBillingQuery(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    Guid? CompanyId = null,
    Guid? TenantSubscriptionId = null,
    Guid? SubscriptionPlanId = null,
    UsageMetric? Metric = null,
    CustomerBillableUsageState? BillingState = null,
    CustomerBillableUsageReason? BillingReason = null);

public sealed record CustomerBillingReasonCount(CustomerBillableUsageReason Reason, long Operations);

public sealed record CustomerBillingSummary(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    long TotalOperations,
    long CalculatedOperations,
    long UnknownOperations,
    long NotBillableOperations,
    long TotalConsumedQuantity,
    long TotalBillableQuantity,
    long CalculatedBillableQuantity,
    decimal CalculatedChargeAmount,
    string Currency,
    IReadOnlyList<CustomerBillingReasonCount> UnknownByReason,
    IReadOnlyList<CustomerBillingReasonCount> NotBillableByReason,
    long CalculatedZeroChargeOperations);

public sealed record CustomerBillingBreakdownRow(
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
    decimal CalculatedChargeAmount,
    string Currency);

public sealed record CustomerBillingAnomalyCounts(
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

public sealed record CustomerBillingReconciliation(
    long TotalOperationsExamined,
    long HealthyOperations,
    long AnomalousOperations,
    CustomerBillingAnomalyCounts AnomalyCounts);

public interface ICustomerBillingReconciliationService
{
    Task<CustomerBillingSummary> GetSummaryAsync(CustomerBillingQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerBillingBreakdownRow>> GetBreakdownAsync(
        CustomerBillingQuery query,
        CustomerBillingGroupBy groupBy,
        CancellationToken cancellationToken);

    Task<CustomerBillingReconciliation> GetReconciliationAsync(
        CustomerBillingQuery query,
        CancellationToken cancellationToken);
}
