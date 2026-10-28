namespace StayFlow.Api.Models;

public enum CustomerBillableUsageState
{
    Calculated = 1,
    NotBillable = 2,
    Unknown = 3
}

public enum CustomerBillableUsageReason
{
    Calculated = 1,
    WithinIncludedAllowance = 2,
    IncludedOnly = 3,
    MetricNotBillable = 4,
    MissingPrice = 5,
    AmbiguousPrice = 6,
    UnsupportedBillingMode = 7,
    MissingEntitlement = 8
}

public sealed class CustomerBillableUsageOperation
{
    private CustomerBillableUsageOperation()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid UsageOperationId { get; private set; }
    public Guid TenantSubscriptionId { get; private set; }
    public Guid SubscriptionPlanId { get; private set; }
    public UsageMetric Metric { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset PeriodStartUtc { get; private set; }
    public DateTimeOffset PeriodEndUtc { get; private set; }
    public long ConsumedQuantity { get; private set; }
    public long UsageBefore { get; private set; }
    public long UsageAfter { get; private set; }
    public long? IncludedQuantity { get; private set; }
    public long BillableQuantity { get; private set; }
    public CustomerBillableUsageState BillingState { get; private set; }
    public CustomerBillableUsageReason BillingReason { get; private set; }
    public Guid? CustomerUsagePriceVersionId { get; private set; }
    public decimal? UnitQuantity { get; private set; }
    public decimal? UnitPrice { get; private set; }
    public decimal? ChargeAmount { get; private set; }
    public string? Currency { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public UsageOperation UsageOperation { get; private set; } = null!;

    public static CustomerBillableUsageOperation Create(
        Guid companyId,
        UsageOperation usageOperation,
        Guid tenantSubscriptionId,
        Guid subscriptionPlanId,
        UsageMetric metric,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset periodEndUtc,
        long consumedQuantity,
        long usageBefore,
        long usageAfter,
        long? includedQuantity,
        long billableQuantity,
        CustomerBillableUsageState billingState,
        CustomerBillableUsageReason billingReason,
        Guid? customerUsagePriceVersionId = null,
        decimal? unitQuantity = null,
        decimal? unitPrice = null,
        decimal? chargeAmount = null,
        string? currency = null)
    {
        ArgumentNullException.ThrowIfNull(usageOperation);

        return new CustomerBillableUsageOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UsageOperationId = usageOperation.Id,
            UsageOperation = usageOperation,
            TenantSubscriptionId = tenantSubscriptionId,
            SubscriptionPlanId = subscriptionPlanId,
            Metric = metric,
            OccurredAtUtc = occurredAtUtc,
            PeriodStartUtc = usageOperation.PeriodStartUtc,
            PeriodEndUtc = periodEndUtc,
            ConsumedQuantity = consumedQuantity,
            UsageBefore = usageBefore,
            UsageAfter = usageAfter,
            IncludedQuantity = includedQuantity,
            BillableQuantity = billableQuantity,
            BillingState = billingState,
            BillingReason = billingReason,
            CustomerUsagePriceVersionId = customerUsagePriceVersionId,
            UnitQuantity = unitQuantity,
            UnitPrice = unitPrice,
            ChargeAmount = chargeAmount,
            Currency = currency,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
