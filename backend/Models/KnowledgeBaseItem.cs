namespace StayFlow.Api.Models;

public sealed class KnowledgeBaseItem : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid? PropertyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public Property? Property { get; set; }
}
