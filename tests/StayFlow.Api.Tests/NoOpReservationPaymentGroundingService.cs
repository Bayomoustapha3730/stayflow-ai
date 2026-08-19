using StayFlow.Api.DTOs.Payments;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Tests;

/// <summary>Test double that reports no reservation payment grounding is available.</summary>
internal sealed class NoOpReservationPaymentGroundingService : IReservationPaymentGroundingService
{
    public Task<ReservationPaymentGroundingDto?> GetReservationPaymentGroundingAsync(
        Guid reservationId,
        Guid companyId,
        CancellationToken cancellationToken) => Task.FromResult<ReservationPaymentGroundingDto?>(null);
}
