using StayFlow.Api.Common;

namespace StayFlow.Api.DTOs.WhatsApp;

public sealed class WhatsAppIntegrationSummaryResponse
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string BusinessPhoneNumberMasked { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsProductionEnabled { get; init; }
    public string Mode { get; init; } = "Development";
    public string HealthStatus { get; init; } = "Unknown";
    public DateTimeOffset? LastHealthCheckAt { get; init; }
    public DateTimeOffset? LastSuccessfulHealthCheckAt { get; init; }
    public DateTimeOffset? LastTemplateSyncAt { get; init; }
    public string? LastErrorSummary { get; init; }
}

public sealed class WhatsAppIntegrationHealthResponse
{
    public Guid IntegrationId { get; init; }
    public string Status { get; init; } = "Unknown";
    public string Message { get; init; } = string.Empty;
    public bool IsSendCapable { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
}

public class WhatsAppTemplateSummaryResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsApproved { get; init; }
    public int VariableCount { get; init; }
    public DateTimeOffset? LastSyncedAt { get; init; }
}

public sealed class WhatsAppTemplateVariableDefinition
{
    public int Position { get; init; }
    public string Placeholder { get; init; } = string.Empty;
}

public sealed class WhatsAppTemplateDetailResponse : WhatsAppTemplateSummaryResponse
{
    public string? HeaderType { get; init; }
    public string BodyText { get; init; } = string.Empty;
    public string? FooterText { get; init; }
    public IReadOnlyCollection<WhatsAppTemplateVariableDefinition> Variables { get; init; } = [];
}

public sealed class WhatsAppTemplateListQuery : PaginationQuery
{
    public string? Status { get; init; }
    public string? Language { get; init; }
    public string? Category { get; init; }
    public string? Search { get; init; }
    public bool? Active { get; init; }
    public bool? ApprovedOnly { get; init; }
}

public sealed class WhatsAppTemplateListResponse
{
    public IReadOnlyCollection<WhatsAppTemplateSummaryResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public sealed class WhatsAppTemplateSyncResponse
{
    public int Added { get; init; }
    public int Updated { get; init; }
    public int Unchanged { get; init; }
    public int Disabled { get; init; }
    public int Failed { get; init; }
    public DateTimeOffset SyncedAt { get; init; }
    public string Status { get; init; } = "Completed";
    public string? Message { get; init; }
}

public sealed class WhatsAppTemplatePreviewRequest
{
    public IReadOnlyCollection<string> Variables { get; init; } = [];
}

public sealed class WhatsAppTemplatePreviewResponse
{
    public string HeaderPreview { get; init; } = string.Empty;
    public string BodyPreview { get; init; } = string.Empty;
    public string FooterPreview { get; init; } = string.Empty;
    public bool HasMissingVariables { get; init; }
}

public sealed class SendWhatsAppTemplateMessageRequest
{
    public string? LanguageCode { get; init; }
    public IReadOnlyCollection<string> Variables { get; init; } = [];
    public string? ClientRequestId { get; init; }
}

public sealed class WhatsAppCustomerServiceWindowStatusResponse
{
    public bool IsOpen { get; init; }
    public DateTimeOffset? LastInboundAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string Reason { get; init; } = string.Empty;
}
