using StayFlow.Api.Models;

namespace StayFlow.Api.Services.Billing;

public enum CustomerBillingDeliveryDispatcherOutcome
{
    Delivered,
    Rejected,
    RetryableFailure,
    Indeterminate,
    NotEligible,
    NotFound,
    ConfigurationError,
    PersistenceFailure,
    StillIndeterminate,
    Unsupported
}

public sealed record CustomerBillingDeliveryDispatcherResult(
    Guid DeliveryId,
    CustomerBillingDeliveryDispatcherOutcome Outcome,
    CustomerBillingDeliveryState? State,
    string ReasonCode,
    string? ExternalObjectId,
    string? ExternalRequestId,
    string? ProviderCode,
    int AttemptCount);

public interface ICustomerBillingDeliveryDispatcher
{
    Task<CustomerBillingDeliveryDispatcherResult> DispatchAsync(
        Guid deliveryId,
        CancellationToken cancellationToken);

    Task<CustomerBillingDeliveryDispatcherResult> ReconcileAsync(
        Guid deliveryId,
        CancellationToken cancellationToken);
}
