namespace StayFlow.Api.Models;

public sealed class ExtraItemRequest : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid ConversationId { get; set; }
    public ExtraItemType ItemType { get; set; }
    public int Quantity { get; set; }
    public string? GuestNote { get; set; }
    public ExtraItemRequestStatus Status { get; set; } = ExtraItemRequestStatus.Pending;

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Reservation Reservation { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
}
