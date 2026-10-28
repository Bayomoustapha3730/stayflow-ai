namespace StayFlow.Api.Models;

public enum CustomerBillingDeliveryProvider
{
    Stripe = 1
}

public enum CustomerBillingDeliveryKind
{
    PendingExternalBilling = 1
}

public enum CustomerBillingDeliveryState
{
    Prepared = 1,
    Dispatching = 2,
    Delivered = 3,
    Failed = 4,
    Indeterminate = 5,
    Suppressed = 6
}

public enum CustomerBillingDeliveryReason
{
    Prepared = 1,
    DispatchClaimed = 2,
    Delivered = 3,
    ZeroChargeSuppressed = 4,
    ProviderRejected = 5,
    ProviderUnavailable = 6,
    Timeout = 7,
    Canceled = 8,
    NetworkFailure = 9,
    PersistenceFailure = 10,
    IndeterminateExternalOutcome = 11,
    ConfigurationMissing = 12,
    InvalidSubscriptionMapping = 13,
    InvalidPriceMapping = 14
}

public enum CustomerBillingDeliveryFailureCategory
{
    ProviderRejected = 1,
    ProviderUnavailable = 2,
    Timeout = 3,
    Canceled = 4,
    NetworkFailure = 5,
    PersistenceFailure = 6,
    ConfigurationMissing = 7,
    InvalidSubscriptionMapping = 8,
    InvalidPriceMapping = 9,
    RetryableProviderUnavailable = 10
}

public sealed class CustomerBillingDelivery
{
    private CustomerBillingDelivery()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid CustomerBillableUsageOperationId { get; private set; }
    public Guid TenantSubscriptionId { get; private set; }
    public Guid SubscriptionPlanId { get; private set; }
    public UsageMetric Metric { get; private set; }
    public DateTimeOffset PeriodStartUtc { get; private set; }
    public DateTimeOffset PeriodEndUtc { get; private set; }
    public CustomerBillingDeliveryProvider DeliveryProvider { get; private set; }
    public CustomerBillingDeliveryKind DeliveryKind { get; private set; }
    public CustomerBillableUsageState BillingStateSnapshot { get; private set; }
    public CustomerBillableUsageReason BillingReasonSnapshot { get; private set; }
    public long BillableQuantitySnapshot { get; private set; }
    public decimal ChargeAmountSnapshot { get; private set; }
    public string CurrencySnapshot { get; private set; } = string.Empty;
    public Guid CustomerUsagePriceVersionId { get; private set; }
    public CustomerBillingDeliveryState DeliveryState { get; private set; }
    public CustomerBillingDeliveryReason DeliveryReason { get; private set; }
    public string ExternalIdempotencyKey { get; private set; } = string.Empty;
    public string? ExternalObjectId { get; private set; }
    public string? ExternalRequestId { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? FirstAttemptAtUtc { get; private set; }
    public DateTimeOffset? LastAttemptAtUtc { get; private set; }
    public DateTimeOffset? DeliveredAtUtc { get; private set; }
    public DateTimeOffset? IndeterminateAtUtc { get; private set; }
    public CustomerBillingDeliveryFailureCategory? LastFailureCategory { get; private set; }
    public string? LastFailureCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    internal static CustomerBillingDelivery Create(
        CustomerBillableUsageOperation operation,
        CustomerBillingDeliveryState state,
        CustomerBillingDeliveryReason reason,
        DateTimeOffset createdAtUtc)
    {
        var deliveryId = Guid.NewGuid();
        return new CustomerBillingDelivery
        {
            Id = deliveryId,
            CompanyId = operation.CompanyId,
            CustomerBillableUsageOperationId = operation.Id,
            TenantSubscriptionId = operation.TenantSubscriptionId,
            SubscriptionPlanId = operation.SubscriptionPlanId,
            Metric = operation.Metric,
            PeriodStartUtc = operation.PeriodStartUtc,
            PeriodEndUtc = operation.PeriodEndUtc,
            DeliveryProvider = CustomerBillingDeliveryProvider.Stripe,
            DeliveryKind = CustomerBillingDeliveryKind.PendingExternalBilling,
            BillingStateSnapshot = operation.BillingState,
            BillingReasonSnapshot = operation.BillingReason,
            BillableQuantitySnapshot = operation.BillableQuantity,
            ChargeAmountSnapshot = operation.ChargeAmount!.Value,
            CurrencySnapshot = operation.Currency!,
            CustomerUsagePriceVersionId = operation.CustomerUsagePriceVersionId!.Value,
            DeliveryState = state,
            DeliveryReason = reason,
            ExternalIdempotencyKey = CreateExternalIdempotencyKey(deliveryId),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    internal static string CreateExternalIdempotencyKey(Guid deliveryId) =>
        $"stayflow:customer-billing:{deliveryId:N}";
}
