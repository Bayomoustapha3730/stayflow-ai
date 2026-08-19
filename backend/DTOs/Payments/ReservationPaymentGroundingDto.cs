namespace StayFlow.Api.DTOs.Payments;

/// <summary>
/// AI-facing, read-only payment snapshot for a reservation.
/// Supports guest concierge questions about payment status, balance, and receipts.
/// Never includes provider secrets, credentials, or unrelated tenant data.
/// </summary>
public sealed class ReservationPaymentGroundingDto
{
    /// <summary>
    /// Reservation ID for reference (not exposed to guest).
    /// </summary>
    public Guid ReservationId { get; init; }

    /// <summary>
    /// Original booking amount for the reservation in the specified currency.
    /// Null when the reservation does not have a booking amount.
    /// </summary>
    public decimal? BookingAmount { get; init; }

    /// <summary>
    /// Currency code (e.g., "KES").
    /// </summary>
    public string Currency { get; init; } = "KES";

    /// <summary>
    /// Sum of all payments with status = Paid.
    /// </summary>
    public decimal TotalPaid { get; init; }

    /// <summary>
    /// max(BookingAmount - TotalPaid, 0). Null when the booking amount is unavailable.
    /// </summary>
    public decimal? RemainingBalance { get; init; }

    /// <summary>
    /// True if at least one payment with status = Paid exists.
    /// </summary>
    public bool HasSuccessfulPayment { get; init; }

    /// <summary>
    /// Number of payment attempts (regardless of status).
    /// </summary>
    public int PaymentCount { get; init; }

    /// <summary>
    /// Status of the most recent payment attempt (by RequestedAtUtc or CreatedAt).
    /// One of: Pending, Processing, Paid, Failed, Cancelled, Expired, or null if no payments exist.
    /// </summary>
    public string? LatestPaymentStatus { get; init; }

    /// <summary>
    /// Amount of the most recent payment attempt.
    /// </summary>
    public decimal? LatestPaymentAmount { get; init; }

    /// <summary>
    /// When the most recent payment was requested (RequestedAtUtc or CreatedAt).
    /// </summary>
    public DateTimeOffset? LatestPaymentRequestedAtUtc { get; init; }

    /// <summary>
    /// When the most recent payment completed (CompletedAtUtc).
    /// Only populated if status is Paid.
    /// </summary>
    public DateTimeOffset? LatestPaymentCompletedAtUtc { get; init; }

    /// <summary>
    /// Payment provider (e.g., "M-PESA", "Stripe").
    /// </summary>
    public string? LatestProvider { get; init; }

    /// <summary>
    /// Payment method (e.g., "STKPush").
    /// </summary>
    public string? LatestPaymentMethod { get; init; }

    /// <summary>
    /// Receipt/transaction ID from the provider for the most recent successful (Paid) payment.
    /// Only populated if a Paid payment exists.
    /// </summary>
    public string? LatestReceiptNumber { get; init; }

    /// <summary>
    /// Guest-friendly failure message from the most recent failed payment.
    /// Only populated if latest payment status is Failed.
    /// </summary>
    public string? LatestFailureMessage { get; init; }
}
