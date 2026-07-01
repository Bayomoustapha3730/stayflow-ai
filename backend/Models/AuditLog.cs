namespace StayFlow.Api.Models;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
