using StayFlow.Api.DTOs.Payments;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services.Payments;

/// <summary>
/// Read-only payment grounding service for AI concierge.
/// Calculates payment facts from persisted payment records.
/// Enforces tenant isolation and never exposes provider secrets.
/// </summary>
public sealed class ReservationPaymentGroundingService(
    IPaymentRepository paymentRepository,
    ILogger<ReservationPaymentGroundingService> logger) : IReservationPaymentGroundingService
{
    public async Task<ReservationPaymentGroundingDto?> GetReservationPaymentGroundingAsync(
        Guid reservationId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var reservation = await paymentRepository.GetReservationForPaymentAsync(
            reservationId,
            companyId,
            cancellationToken);

        if (reservation is null)
        {
            logger.LogWarning(
                "Payment grounding denied: reservation {ReservationId} does not belong to company {CompanyId}.",
                reservationId,
                companyId);
            return null;
        }

        // Fetch all payments for this reservation (tenant-scoped)
        var payments = await paymentRepository.GetByReservationIdAsync(
            reservationId,
            companyId,
            cancellationToken);

        return BuildGroundingSnapshot(reservation, payments);
    }

    private static ReservationPaymentGroundingDto BuildGroundingSnapshot(
        Reservation reservation,
        IReadOnlyCollection<Payment> payments)
    {
        var paidPayments = payments
            .Where(payment => payment.Status.ToPaymentStatus() == PaymentStatus.Paid)
            .ToList();

        var totalPaid = paidPayments.Sum(p => p.Amount);
        decimal? remainingBalance = reservation.BookingAmount is { } bookingAmount
            ? Math.Max(bookingAmount - totalPaid, 0)
            : null;

        // Get the latest payment (most recent by RequestedAtUtc, then CreatedAt)
        var latestPayment = payments
            .OrderByDescending(p => p.RequestedAtUtc ?? p.CreatedAt)
            .FirstOrDefault();

        var latestReceiptNumber = paidPayments.Count > 0
            ? paidPayments
                .OrderByDescending(p => p.CompletedAtUtc ?? p.CreatedAt)
                .FirstOrDefault()?.ProviderTransactionId
            : null;

        return new ReservationPaymentGroundingDto
        {
            ReservationId = reservation.Id,
            BookingAmount = reservation.BookingAmount,
            Currency = reservation.Currency ?? payments.FirstOrDefault()?.Currency ?? "KES",
            TotalPaid = totalPaid,
            RemainingBalance = remainingBalance,
            HasSuccessfulPayment = paidPayments.Count > 0,
            PaymentCount = payments.Count,
            LatestPaymentStatus = latestPayment?.Status,
            LatestPaymentAmount = latestPayment?.Amount,
            LatestPaymentRequestedAtUtc = latestPayment?.RequestedAtUtc ?? latestPayment?.CreatedAt,
            LatestPaymentCompletedAtUtc = latestPayment?.Status is { } status && status.ToPaymentStatus() == PaymentStatus.Paid
                ? latestPayment.CompletedAtUtc
                : null,
            LatestProvider = latestPayment?.Provider,
            LatestPaymentMethod = latestPayment?.PaymentMethod,
            LatestReceiptNumber = latestReceiptNumber,
            LatestFailureMessage = latestPayment?.Status is { } failureStatus && failureStatus.ToPaymentStatus() == PaymentStatus.Failed
                ? latestPayment.FailureMessage
                : null
        };
    }
}
