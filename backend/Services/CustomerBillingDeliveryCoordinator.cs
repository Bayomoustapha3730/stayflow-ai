using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class CustomerBillingDeliveryCoordinator(ICustomerBillingDeliveryStore deliveryStore)
    : ICustomerBillingDeliveryCoordinator
{
    public async Task<CustomerBillingDeliveryPreparationResult> PrepareAsync(
        Guid billingOperationId,
        DateTimeOffset preparedAtUtc,
        CancellationToken cancellationToken)
    {
        var operation = await deliveryStore.GetBillingOperationAsync(billingOperationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer billing operation '{billingOperationId}' was not found.");

        if (operation.BillingState != CustomerBillableUsageState.Calculated
            || operation.ChargeAmount is null
            || operation.Currency != "USD"
            || operation.CustomerUsagePriceVersionId is null
            || operation.BillableQuantity < 0)
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
}
