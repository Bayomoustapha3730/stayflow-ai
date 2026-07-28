namespace StayFlow.Api.Models;

public sealed class ConversationMessageKnowledgeSource : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid ConversationMessageId { get; set; }
    public Guid PropertyKnowledgeArticleId { get; set; }
    public int Rank { get; set; }
    public bool IsPrimary { get; set; }
    public string? RelevanceReason { get; set; }

    public Company Company { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
    public ConversationMessage ConversationMessage { get; set; } = null!;
    public PropertyKnowledgeArticle PropertyKnowledgeArticle { get; set; } = null!;
}