using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public enum CustomerBillingDeliveryPreparationStatus
{
    NotExternallyBillable = 1,
    Prepared = 2,
    Suppressed = 3
}

public sealed record CustomerBillingDeliveryPreparationResult(
    CustomerBillingDeliveryPreparationStatus Status,
    CustomerBillingDelivery? Delivery);

public interface ICustomerBillingDeliveryStore
{
    Task<CustomerBillableUsageOperation?> GetBillingOperationAsync(
        Guid billingOperationId,
        CancellationToken cancellationToken);

    Task<CustomerBillingDelivery> PrepareAsync(Guid billingOperationId, DateTimeOffset preparedAtUtc);

    Task<CustomerBillingDelivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken);

    Task<CustomerBillingDelivery?> GetByBillingOperationAsync(
        Guid billingOperationId,
        CancellationToken cancellationToken);

    Task<bool> TryStartDispatchAsync(Guid deliveryId, DateTimeOffset occurredAtUtc);

    Task<bool> RecordDeliveredAsync(
        Guid deliveryId,
        string? externalObjectId,
        string? externalRequestId,
        DateTimeOffset occurredAtUtc);

    Task<bool> RecordFailedAsync(
        Guid deliveryId,
        CustomerBillingDeliveryFailureCategory failureCategory,
        string failureCode,
        string? externalRequestId,
        DateTimeOffset occurredAtUtc);

    Task<bool> RecordIndeterminateAsync(
        Guid deliveryId,
        string? externalObjectId,
        string? externalRequestId,
        string? failureCode,
        DateTimeOffset occurredAtUtc);

    Task<bool> RecordReconciledDeliveredAsync(
        Guid deliveryId,
        string? externalObjectId,
        string? externalRequestId,
        DateTimeOffset occurredAtUtc);

    Task<bool> RecordReconciledRejectedAsync(
        Guid deliveryId,
        string failureCode,
        string? externalRequestId,
        DateTimeOffset occurredAtUtc);
}

public interface ICustomerBillingDeliveryCoordinator
{
    Task<CustomerBillingDeliveryPreparationResult> PrepareAsync(
        Guid billingOperationId,
        DateTimeOffset preparedAtUtc,
        CancellationToken cancellationToken);
}
