using StayFlow.Api.Models;

namespace StayFlow.Api.Services.Payments;

/// <summary>
/// Fires guest-facing/host-facing side effects once a payment has been persisted as Paid.
/// Never throws: failures here must never roll back or affect the already-authoritative payment
/// record. Callers should invoke this after the Paid transition has been durably saved.
/// </summary>
public interface IPostPaymentNotificationService
{
    Task NotifyPaymentPaidAsync(Payment payment, CancellationToken cancellationToken);
}
