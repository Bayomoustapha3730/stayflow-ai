namespace StayFlow.Api.Models;

public sealed class PendingConciergeAction : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid? ReservationId { get; set; }
    public ConciergeActionType ActionType { get; set; }
    public string SerializedNormalizedParameters { get; set; } = "{}";
    public PendingConciergeActionStatus Status { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? FailureReasonCode { get; set; }
    public Guid CreatedFromMessageId { get; set; }

    public Company Company { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Reservation? Reservation { get; set; }
}
