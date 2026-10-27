namespace StayFlow.Api.Models;

public enum CopilotOperationType
{
    CopilotSuggestion = 1,
    CopilotGeneratedReply = 2,
    WorkspaceDraft = 3
}

public enum CopilotOperationStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    InProgress = 4,
    Indeterminate = 5
}

public sealed class CopilotOperation : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid ActorUserId { get; set; }
    public CopilotOperationType OperationType { get; set; }
    public CopilotOperationStatus Status { get; set; } = CopilotOperationStatus.Pending;
    public string? SemanticRequestFingerprint { get; set; }
    public DateTimeOffset? DispatchStartedAt { get; set; }
    public string? BusinessResultJson { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Company Company { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
    public User ActorUser { get; set; } = null!;
}
