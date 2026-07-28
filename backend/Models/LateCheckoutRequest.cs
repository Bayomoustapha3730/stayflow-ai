namespace StayFlow.Api.Models;

public sealed class LateCheckoutRequest : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid ConversationId { get; set; }
    public TimeOnly? RequestedTime { get; set; }
    public string? GuestNote { get; set; }
    public LateCheckoutRequestStatus Status { get; set; } = LateCheckoutRequestStatus.Pending;
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? DecisionNote { get; set; }

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Reservation Reservation { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
}
