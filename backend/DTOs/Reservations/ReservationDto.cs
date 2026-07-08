namespace StayFlow.Api.DTOs.Reservations;

public sealed class ReservationDto
{
    public Guid Id { get; init; }
    public Guid PropertyId { get; init; }
    public Guid PrimaryGuestId { get; init; }
    public string? ExternalReservationReference { get; init; }
    public string ReservationSource { get; init; } = string.Empty;
    public string? ConfirmationNumber { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public int Adults { get; init; }
    public int Children { get; init; }
    public int TotalGuestCount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Currency { get; init; }
    public decimal? BookingAmount { get; init; }
    public string? SpecialRequests { get; init; }
    public string? InternalNotes { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
