namespace StayFlow.Api.DTOs.WhatsApp;

/// <summary>
/// Non-secret routing metadata accepted from create/update integration requests.
/// Intentionally excludes access tokens, app secrets, and webhook verify tokens.
/// </summary>
public sealed class WhatsAppIntegrationConfigurationRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string PhoneNumberId { get; init; } = string.Empty;
    public string WhatsAppBusinessAccountId { get; init; } = string.Empty;
    public string BusinessPhoneNumberMasked { get; init; } = string.Empty;
    public string? CredentialReference { get; init; }
    public string GraphApiVersion { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}

public sealed class WhatsAppIntegrationDetailResponse
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string PhoneNumberId { get; init; } = string.Empty;
    public string WhatsAppBusinessAccountId { get; init; } = string.Empty;
    public string BusinessPhoneNumberMasked { get; init; } = string.Empty;
    public string? CredentialReference { get; init; }
    public string GraphApiVersion { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsProductionEnabled { get; init; }
    public string Mode { get; init; } = "Development";
    public string HealthStatus { get; init; } = "Unknown";
    public DateTimeOffset? LastHealthCheckAt { get; init; }
    public DateTimeOffset? LastSuccessfulHealthCheckAt { get; init; }
    public DateTimeOffset? LastTemplateSyncAt { get; init; }
    public string? LastErrorSummary { get; init; }
}

public sealed class WhatsAppProductionEnableResponse
{
    public Guid IntegrationId { get; init; }
    public bool IsProductionEnabled { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset CheckedAt { get; init; }
}
