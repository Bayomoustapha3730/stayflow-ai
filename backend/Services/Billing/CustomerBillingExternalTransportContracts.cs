using StayFlow.Api.Models;

namespace StayFlow.Api.Services.Billing;

public enum CustomerBillingExternalTransportOutcome
{
    Delivered,
    Rejected,
    RetryableUnavailable,
    Indeterminate,
    ConfigurationError,
    Disabled,
    Simulated
}

public enum CustomerBillingExternalReconciliationOutcome
{
    FoundDelivered,
    FoundRejected,
    NotFound,
    StillIndeterminate,
    Unsupported
}

public sealed record CustomerBillingExternalMapping(
    string Transport,
    string MeterEventName,
    string CustomerPayloadKey,
    string ValuePayloadKey,
    bool BillableQuantityIsExternalUnit);

public sealed record CustomerBillingExternalRequest(
    Guid DeliveryId,
    Guid CompanyId,
    Guid TenantSubscriptionId,
    Guid SubscriptionPlanId,
    UsageMetric Metric,
    long BillableQuantity,
    decimal ChargeAmount,
    string Currency,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    string ExternalIdempotencyKey,
    CustomerBillingExternalMapping Mapping)
{
    public static CustomerBillingExternalRequest FromDelivery(
        CustomerBillingDelivery delivery,
        CustomerBillingExternalMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(mapping);
        return new CustomerBillingExternalRequest(
            delivery.Id,
            delivery.CompanyId,
            delivery.TenantSubscriptionId,
            delivery.SubscriptionPlanId,
            delivery.Metric,
            delivery.BillableQuantitySnapshot,
            delivery.ChargeAmountSnapshot,
            delivery.CurrencySnapshot,
            delivery.PeriodStartUtc,
            delivery.PeriodEndUtc,
            delivery.ExternalIdempotencyKey,
            mapping);
    }
}

public sealed record CustomerBillingExternalResult(
    CustomerBillingExternalTransportOutcome Outcome,
    string ReasonCode,
    string? ExternalObjectId = null,
    string? ExternalRequestId = null);

public sealed record CustomerBillingExternalReconciliationRequest(
    string ExternalIdempotencyKey,
    string? ExternalObjectId,
    Guid DeliveryId);

public sealed record CustomerBillingExternalReconciliationResult(
    CustomerBillingExternalReconciliationOutcome Outcome,
    string ReasonCode,
    string? ExternalObjectId = null,
    string? ExternalRequestId = null);

public interface ICustomerBillingExternalTransport
{
    Task<CustomerBillingExternalResult> SendAsync(
        CustomerBillingExternalRequest request,
        CancellationToken cancellationToken);

    Task<CustomerBillingExternalReconciliationResult> ReconcileAsync(
        CustomerBillingExternalReconciliationRequest request,
        CancellationToken cancellationToken);
}
