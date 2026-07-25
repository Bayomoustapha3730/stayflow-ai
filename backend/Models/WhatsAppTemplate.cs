namespace StayFlow.Api.Models;

public sealed class WhatsAppTemplate : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid WhatsAppIntegrationId { get; set; }
    public string ExternalTemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? HeaderType { get; set; }
    public string BodyText { get; set; } = string.Empty;
    public string? FooterText { get; set; }
    public int VariableCount { get; set; }
    public string ComponentsJson { get; set; } = "[]";
    public DateTimeOffset? LastSyncedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public WhatsAppIntegration WhatsAppIntegration { get; set; } = null!;
}
