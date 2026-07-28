namespace StayFlow.Api.Models;

public sealed class HousekeepingRequest : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid ConversationId { get; set; }
    public HousekeepingRequestType RequestType { get; set; }
    public DateOnly? RequestedForDate { get; set; }
    public string? GuestNote { get; set; }
    public HousekeepingRequestStatus Status { get; set; } = HousekeepingRequestStatus.Pending;

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Reservation Reservation { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
}
