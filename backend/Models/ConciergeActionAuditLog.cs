namespace StayFlow.Api.Models;

public sealed class ConciergeActionAuditLog : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid? PendingActionId { get; set; }
    public ConciergeActionType ActionType { get; set; }
    public ConciergeActionAuditEventType EventType { get; set; }
    public string ActorType { get; set; } = "System";
    public Guid? ActorUserId { get; set; }
    public string Channel { get; set; } = "Web";
    public string ResultCode { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }

    public Company Company { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
    public PendingConciergeAction? PendingAction { get; set; }
}
