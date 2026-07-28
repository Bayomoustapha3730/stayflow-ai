namespace StayFlow.Api.Models;

public sealed class WhatsAppIntegration : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string WhatsAppBusinessAccountId { get; set; } = string.Empty;
    public string BusinessPhoneNumberMasked { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsProductionEnabled { get; set; }
    public string GraphApiVersion { get; set; } = string.Empty;
    public string? CredentialReference { get; set; }
    public string WebhookConfigurationStatus { get; set; } = "Unknown";
    public string TemplateSyncStatus { get; set; } = "NotStarted";
    public DateTimeOffset? LastHealthCheckAt { get; set; }
    public DateTimeOffset? LastSuccessfulHealthCheckAt { get; set; }
    public DateTimeOffset? LastTemplateSyncAt { get; set; }
    public string? LastErrorSummary { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<WhatsAppTemplate> Templates { get; set; } = [];
}