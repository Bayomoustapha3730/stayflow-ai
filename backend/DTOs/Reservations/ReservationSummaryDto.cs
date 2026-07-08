namespace StayFlow.Api.DTOs.Reservations;

public sealed class ReservationSummaryDto
{
    public Guid Id { get; init; }
    public Guid PropertyId { get; init; }
    public Guid PrimaryGuestId { get; init; }
    public string ReservationSource { get; init; } = string.Empty;
    public string? ConfirmationNumber { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public int TotalGuestCount { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
