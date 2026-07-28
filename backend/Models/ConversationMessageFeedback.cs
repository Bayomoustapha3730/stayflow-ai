namespace StayFlow.Api.Models;

public sealed class ConversationMessageFeedback : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid ConversationMessageId { get; set; }
    public Guid GuestId { get; set; }
    public ConversationMessageFeedbackValue FeedbackValue { get; set; }
    public string? Comment { get; set; }

    public Company Company { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
    public ConversationMessage ConversationMessage { get; set; } = null!;
}