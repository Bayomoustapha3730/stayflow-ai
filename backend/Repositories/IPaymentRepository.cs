using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IPaymentRepository
{
    Task<Reservation?> GetReservationForPaymentAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken);

    Task<Payment?> GetByExternalReferenceAsync(string externalReference, Guid companyId, CancellationToken cancellationToken);

    /// <summary>Tenant-scoped single payment lookup. Returns null for cross-tenant IDs.</summary>
    Task<Payment?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken);

    /// <summary>Tenant-scoped payments for a reservation, most recent first.</summary>
    Task<IReadOnlyCollection<Payment>> GetByReservationIdAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken);

    /// <summary>
    /// Callback correlation lookup. Intentionally NOT tenant-scoped: the caller (anonymous webhook)
    /// has no tenant context, and CompanyId is derived from the matched Payment, never trusted from the callback.
    /// </summary>
    Task<Payment?> GetByCheckoutRequestIdAsync(string checkoutRequestId, CancellationToken cancellationToken);

    Task<bool> ReservationBelongsToCompanyAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken);

    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    /// <summary>Records a provider webhook event for idempotency. Returns false if the event was already processed.</summary>
    Task<bool> TryRecordWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken);

    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
