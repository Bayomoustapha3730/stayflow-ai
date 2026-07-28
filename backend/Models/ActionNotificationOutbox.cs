namespace StayFlow.Api.Models;

public enum ActionNotificationOutboxStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public sealed class ActionNotificationOutbox : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ActionId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string PayloadReference { get; set; } = string.Empty;
    public ActionNotificationOutboxStatus Status { get; set; } = ActionNotificationOutboxStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? LastFailureCode { get; set; }

    public Company Company { get; set; } = null!;
}
