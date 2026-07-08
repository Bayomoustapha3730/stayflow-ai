namespace StayFlow.Api.Models;

public sealed class Reservation : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid PrimaryGuestId { get; set; }
    public string? ExternalReservationReference { get; set; }
    public string ReservationSource { get; set; } = "Manual";
    public string? ConfirmationNumber { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public int TotalGuestCount { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Draft;
    public string? Currency { get; set; }
    public decimal? BookingAmount { get; set; }
    public string? SpecialRequests { get; set; }
    public string? InternalNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Guest PrimaryGuest { get; set; } = null!;
}
