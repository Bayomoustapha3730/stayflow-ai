using StayFlow.Api.DTOs.Payments;

namespace StayFlow.Api.Services.Payments;

/// <summary>
/// Read-only payment grounding service for AI concierge.
/// Provides tenant-safe payment information for reservations.
/// Never permits payment modifications, cross-tenant access, or exposure of secrets.
/// </summary>
public interface IReservationPaymentGroundingService
{
    /// <summary>
    /// Get payment grounding information for a reservation.
    /// Returns null if reservation does not exist or does not belong to the tenant.
    /// Returns a snapshot with zero payments if reservation exists but has no payments.
    /// </summary>
    Task<ReservationPaymentGroundingDto?> GetReservationPaymentGroundingAsync(
        Guid reservationId,
        Guid companyId,
        CancellationToken cancellationToken);
}
