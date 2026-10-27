using StayFlow.Api.DTOs.AIProvider;

namespace StayFlow.Api.Models;

public sealed class AiProviderCallTelemetry : AuditableEntity
{
    public string ProviderCallId { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public OuterOperationType? OuterOperationType { get; set; }
    public Guid? OuterOperationId { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public bool UsageKnown { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? TotalTokens { get; set; }
    public AIProviderOutcome Outcome { get; set; }

    public Company Company { get; set; } = null!;
}
