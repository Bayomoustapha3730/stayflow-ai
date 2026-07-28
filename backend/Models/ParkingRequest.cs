namespace StayFlow.Api.Models;

public sealed class ParkingRequest : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid ConversationId { get; set; }
    public int VehicleCount { get; set; }
    public string? VehicleDescription { get; set; }
    public DateOnly? RequestedFromDate { get; set; }
    public DateOnly? RequestedToDate { get; set; }
    public string? GuestNote { get; set; }
    public ParkingRequestStatus Status { get; set; } = ParkingRequestStatus.Pending;

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Reservation Reservation { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
}
