namespace StayFlow.Api.DTOs.PlatformAdmin;

public sealed class PlatformTenantSummaryDto
{
    public Guid CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? SubscriptionStatus { get; init; }
    public int UserCount { get; init; }
    public int PropertyCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class PlatformSaasMetricsDto
{
    public int ActiveTenants { get; init; }
    public int TrialTenants { get; init; }
    public int PaidTenants { get; init; }
    public decimal MrrEstimate { get; init; }
    public decimal ArrEstimate { get; init; }
    public int ChurnEventsLast30Days { get; init; }
    public int FailedPaymentsLast30Days { get; init; }
    public long AiUsageLast30Days { get; init; }
    public long WhatsAppUsageLast30Days { get; init; }
    public int PropertyCount { get; init; }
    public int UserCount { get; init; }
    public DateTimeOffset DataFreshAtUtc { get; init; }
}