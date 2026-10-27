using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Services;

namespace StayFlow.Api.Models;

public sealed class AiProviderCallTelemetry : AuditableEntity
{
    public string ProviderCallId { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public OuterOperationType? OuterOperationType { get; set; }
    public Guid? OuterOperationId { get; set; }
    public string? Provider { get; set; }
    public string? RequestedModel { get; set; }
    public string? ResolvedModel { get; set; }
    public string? ProviderRequestId { get; set; }
    public AiProviderCallLifecycleState LifecycleState { get; set; }
    public DateTimeOffset? DispatchStartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureCategory { get; set; }
    public bool UsageKnown { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? TotalTokens { get; set; }
    public AIProviderOutcome? Outcome { get; set; }
    public AiProviderCostState CostState { get; set; } = AiProviderCostState.Unknown;
    public AiProviderCostReason CostReason { get; set; } = AiProviderCostReason.ProviderUsageUnavailable;
    public Guid? PricingVersionId { get; set; }
    public decimal? ProviderCostAmount { get; set; }
    public string? ProviderCostCurrency { get; set; }

    public Company Company { get; set; } = null!;
    public AiProviderPricingVersion? PricingVersion { get; set; }
}
