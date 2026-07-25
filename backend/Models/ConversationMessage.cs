namespace StayFlow.Api.Models;

public sealed class ConversationMessage : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ConversationId { get; set; }
    public ConversationSenderType SenderType { get; set; }
    public string Content { get; set; } = string.Empty;
    public ConversationMessageType MessageType { get; set; } = ConversationMessageType.Text;
    public string? ExternalMessageId { get; set; }
    public ConversationMessageProvider Provider { get; set; } = ConversationMessageProvider.None;
    public ConversationMessageDeliveryStatus? DeliveryStatus { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderModel { get; set; }
    public string? ProviderRequestId { get; set; }
    public string? AIOutcome { get; set; }
    public string? FailureCategory { get; set; }
    public string? EscalationReason { get; set; }
    public bool IsInternal { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public Company Company { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
}
