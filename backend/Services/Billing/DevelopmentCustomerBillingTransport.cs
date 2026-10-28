namespace StayFlow.Api.Services.Billing;

public sealed class DevelopmentCustomerBillingTransport : ICustomerBillingExternalTransport
{
    public Task<CustomerBillingExternalResult> SendAsync(
        CustomerBillingExternalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CustomerBillingExternalResult(
            CustomerBillingExternalTransportOutcome.Simulated,
            "development_transport_no_external_call",
            $"simulated:{request.DeliveryId:N}"));
    }

    public Task<CustomerBillingExternalReconciliationResult> ReconcileAsync(
        CustomerBillingExternalReconciliationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CustomerBillingExternalReconciliationResult(
            CustomerBillingExternalReconciliationOutcome.Unsupported,
            "development_transport_has_no_external_state"));
    }
}
