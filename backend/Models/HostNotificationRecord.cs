namespace StayFlow.Api.Models;

public sealed class HostNotificationRecord : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid ConversationId { get; set; }
    public HostNotificationReasonCode ReasonCode { get; set; }
    public HostNotificationPriority Priority { get; set; } = HostNotificationPriority.Normal;
    public string? GuestNote { get; set; }

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Reservation? Reservation { get; set; }
    public Conversation Conversation { get; set; } = null!;
}
