namespace StayFlow.Api.Models;

public enum HostCopilotSlaAlertStatus
{
    Open = 0,
    Resolved = 1
}

public sealed class HostCopilotSlaAlert : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid? ReservationId { get; set; }
    public HostNotificationPriority Priority { get; set; } = HostNotificationPriority.Normal;
    public bool IsEmergency { get; set; }
    public DateTimeOffset TriggeredAt { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset LastGuestMessageAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public HostCopilotSlaAlertStatus Status { get; set; } = HostCopilotSlaAlertStatus.Open;

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
    public Reservation? Reservation { get; set; }
}
