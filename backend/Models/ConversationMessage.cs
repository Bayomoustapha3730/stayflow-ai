namespace StayFlow.Api.Models;

public sealed class ConversationMessage : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ConversationId { get; set; }
    public string SenderType { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? AIOutcome { get; set; }
    public string? EscalationReason { get; set; }
    public bool IsInternal { get; set; }

    public Company Company { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
}
