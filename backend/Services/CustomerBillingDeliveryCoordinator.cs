using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class CustomerBillingDeliveryCoordinator(ICustomerBillingDeliveryStore deliveryStore)
    : ICustomerBillingDeliveryCoordinator
{
    public async Task<CustomerBillingDeliveryPreparationResult> PrepareAsync(
        CustomerBillableUsageOperation operation,
        DateTimeOffset preparedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!IsExternallyBillable(operation))
        {
            return new CustomerBillingDeliveryPreparationResult(
                CustomerBillingDeliveryPreparationStatus.NotExternallyBillable,
                null);
        }

        var delivery = await deliveryStore.StageAsync(operation, preparedAtUtc, cancellationToken);
        return new CustomerBillingDeliveryPreparationResult(
            delivery!.DeliveryState == CustomerBillingDeliveryState.Suppressed
                ? CustomerBillingDeliveryPreparationStatus.Suppressed
                : CustomerBillingDeliveryPreparationStatus.Prepared,
            delivery);
    }

    public async Task<CustomerBillingDeliveryPreparationResult> PrepareAsync(
        Guid billingOperationId,
        DateTimeOffset preparedAtUtc,
        CancellationToken cancellationToken)
    {
        var operation = await deliveryStore.GetBillingOperationAsync(billingOperationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer billing operation '{billingOperationId}' was not found.");

        if (!IsExternallyBillable(operation))
        {
            return new CustomerBillingDeliveryPreparationResult(
                CustomerBillingDeliveryPreparationStatus.NotExternallyBillable,
                null);
        }

        var delivery = await deliveryStore.PrepareAsync(billingOperationId, preparedAtUtc);
        return new CustomerBillingDeliveryPreparationResult(
            delivery.DeliveryState == CustomerBillingDeliveryState.Suppressed
                ? CustomerBillingDeliveryPreparationStatus.Suppressed
                : CustomerBillingDeliveryPreparationStatus.Prepared,
            delivery);
    }

    private static bool IsExternallyBillable(CustomerBillableUsageOperation operation) =>
        operation.BillingState == CustomerBillableUsageState.Calculated
        && operation.BillingReason == CustomerBillableUsageReason.Calculated
        && operation.ChargeAmount is not null
        && operation.Currency == "USD"
        && operation.CustomerUsagePriceVersionId is not null
        && operation.BillableQuantity > 0;
}
