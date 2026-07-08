namespace StayFlow.Api.DTOs.Reservations;

public sealed class CreateReservationRequest
{
    public Guid PropertyId { get; init; }
    public Guid PrimaryGuestId { get; init; }
    public string? ExternalReservationReference { get; init; }
    public string ReservationSource { get; init; } = "Manual";
    public string? ConfirmationNumber { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public int Adults { get; init; } = 1;
    public int Children { get; init; }
    public string? Currency { get; init; }
    public decimal? BookingAmount { get; init; }
    public string? SpecialRequests { get; init; }
    public string? InternalNotes { get; init; }
}
