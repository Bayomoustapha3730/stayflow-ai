using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Payments;

namespace StayFlow.Api.Services.Payments;

public interface IPaymentService
{
    Task<ApiResponse<PaymentDto>> InitiateMpesaPaymentAsync(
        InitiateMpesaPaymentRequest request,
        CancellationToken cancellationToken);

    /// <summary>Tenant-scoped single payment lookup.</summary>
    Task<ApiResponse<PaymentDto>> GetPaymentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tenant-scoped payments for a reservation belonging to the active tenant.</summary>
    Task<ApiResponse<IReadOnlyCollection<PaymentDto>>> GetReservationPaymentsAsync(Guid reservationId, CancellationToken cancellationToken);

    /// <summary>
    /// Tenant-scoped, grounded payment summary (booking amount, total paid, remaining balance)
    /// for a reservation. Backend source of truth for host "Paid in Full" / "Balance Due" UI.
    /// </summary>
    Task<ApiResponse<ReservationPaymentGroundingDto>> GetReservationPaymentSummaryAsync(Guid reservationId, CancellationToken cancellationToken);

    /// <summary>
    /// Processes an M-PESA STK Push callback. Tenant-neutral by design: the caller is anonymous,
    /// and CompanyId is derived exclusively from the correlated PaymentTransaction, never from the payload.
    /// Idempotent: repeated callbacks for the same provider event are safely ignored.
    /// </summary>
    /// <param name="rawBody">Raw callback JSON as received from Safaricom, used for parsing and duplicate-detection hashing.</param>
    Task<MpesaCallbackResult> HandleMpesaCallbackAsync(string rawBody, CancellationToken cancellationToken);
}

public enum MpesaCallbackResult
{
    Processed,
    DuplicateIgnored,
    UnknownCheckoutRequestIgnored,
    MalformedIgnored
}
