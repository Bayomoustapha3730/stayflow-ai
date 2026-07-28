namespace StayFlow.Api.Models;

public sealed class PropertyKnowledgeArticle : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyKnowledgeCategory Category { get; set; } = PropertyKnowledgeCategory.Other;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsApproved { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
    public User? CreatedByUser { get; set; }
    public User? UpdatedByUser { get; set; }
    public User? DeletedByUser { get; set; }
    public ICollection<ConversationMessageKnowledgeSource> ConversationMessageSources { get; set; } = [];
}
