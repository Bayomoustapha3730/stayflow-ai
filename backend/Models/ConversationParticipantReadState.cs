namespace StayFlow.Api.Models;

public sealed class ConversationParticipantReadState : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ConversationId { get; set; }
    public ConversationParticipantKind ParticipantKind { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid? LastReadMessageId { get; set; }
    public DateTimeOffset LastReadAt { get; set; }

    public Company Company { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
}
