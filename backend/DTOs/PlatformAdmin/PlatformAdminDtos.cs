namespace StayFlow.Api.DTOs.PlatformAdmin;

public sealed class PlatformTenantQueryDto
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed class PlatformTenantPagedResultDto
{
    public IReadOnlyCollection<PlatformTenantSummaryDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

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

public sealed class PlatformTenantDetailDto
{
    public Guid CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string? SubscriptionStatus { get; init; }
    public string? CurrentPlanName { get; init; }
    public int UserCount { get; init; }
    public int PropertyCount { get; init; }
    public int ConversationCount { get; init; }
    public long AiUsageLast30Days { get; init; }
    public long ApiUsageLast30Days { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class PlatformTenantActionRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class PlatformTenantRepairRequest
{
    public bool NormalizeStatusAndActivation { get; init; } = true;
    public bool RecomputeSubscriptionSnapshot { get; init; } = true;
    public string Reason { get; init; } = string.Empty;
}

public sealed class PlatformSubscriptionSyncRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class PlatformTenantLifecycleAuditDto
{
    public Guid AuditLogId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class PlatformOrganizationHealthDto
{
    public Guid CompanyId { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int ActiveUserCount { get; init; }
    public int ActiveOwnerOrAdminCount { get; init; }
    public int ActivePropertyCount { get; init; }
    public int OpenConversations { get; init; }
    public int OverdueActionCount { get; init; }
    public bool HasBlockingIssues { get; init; }
    public IReadOnlyCollection<string> HealthSignals { get; init; } = [];
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

public sealed class PlatformUsageOverviewDto
{
    public long ApiRequestsLast30Days { get; init; }
    public long AiRequestsLast30Days { get; init; }
    public long AiTokensLast30Days { get; init; }
    public long WhatsAppMessagesLast30Days { get; init; }
    public long ReservationsLast30Days { get; init; }
    public long FileUploadsLast30Days { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
}

public sealed class PlatformFeatureFlagDto
{
    public string PlanName { get; init; } = string.Empty;
    public Guid PlanId { get; init; }
    public string Key { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public bool IsUnlimited { get; init; }
    public long? QuotaLimit { get; init; }
    public string? Unit { get; init; }
    public string? Notes { get; init; }
}

public sealed class PlatformUpdateFeatureFlagRequest
{
    public bool IsEnabled { get; init; }
    public long? QuotaLimit { get; init; }
    public bool? IsUnlimited { get; init; }
    public string? Unit { get; init; }
    public string? Notes { get; init; }
}

public sealed class PlatformOperationalMetricsDto
{
    public long ApiRequestsLast30Days { get; init; }
    public long SignalREventsLast30Days { get; init; }
    public long AiRequestsLast30Days { get; init; }
    public int BillingWebhookEventsLast30Days { get; init; }
    public int EmailEventsLast30Days { get; init; }
    public long WhatsAppMessagesLast30Days { get; init; }
    public int BackgroundJobRetriesLast30Days { get; init; }
    public int DatabaseHealthScore { get; init; }
    public int QueueDepthEstimate { get; init; }
    public int HealthCheckIssuesLast24Hours { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
}

public sealed class PlatformBackgroundJobStatusDto
{
    public string JobName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? LastObservedAtUtc { get; init; }
    public int FailureCountLast24Hours { get; init; }
}

public sealed class PlatformWebhookMonitoringDto
{
    public string Provider { get; init; } = string.Empty;
    public int TotalEventsLast24Hours { get; init; }
    public int DuplicatesLast24Hours { get; init; }
    public int FailedInvoiceEventsLast24Hours { get; init; }
    public DateTimeOffset? LatestProcessedAtUtc { get; init; }
}

public sealed class PlatformQueueMonitoringDto
{
    public string QueueName { get; init; } = string.Empty;
    public int DepthEstimate { get; init; }
    public string Notes { get; init; } = string.Empty;
}

public sealed class PlatformEmailDeliveryMonitoringDto
{
    public int PasswordResetIssuedLast24Hours { get; init; }
    public int EmailVerificationIssuedLast24Hours { get; init; }
    public int ExpiredEmailTokensLast24Hours { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
}

public sealed class PlatformProviderHealthDto
{
    public string Provider { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset CheckedAtUtc { get; init; }
}

public sealed class PlatformBillingHealthDto
{
    public int ActiveSubscriptions { get; init; }
    public int PastDueSubscriptions { get; init; }
    public int FailedInvoicesLast30Days { get; init; }
    public int WebhookEventsLast24Hours { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
}

public sealed class PlatformReadOnlyDiagnosticDto
{
    public string Area { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class PlatformSystemConfigurationDto
{
    public string EnvironmentName { get; init; } = string.Empty;
    public string AiProvider { get; init; } = string.Empty;
    public bool OpenAiConfigured { get; init; }
    public int AuthRateLimitPerMinute { get; init; }
    public int HostApiRateLimitPerMinute { get; init; }
    public int AiGenerationRateLimitPerMinute { get; init; }
    public bool BillingWebhookEnabled { get; init; }
    public bool WhatsAppWebhookEnabled { get; init; }
}

public sealed class PlatformSupportImpersonationStartRequest
{
    public Guid TargetCompanyId { get; init; }
    public Guid TargetUserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string ExplicitAuthorizationCode { get; init; } = string.Empty;
}

public sealed class PlatformSupportImpersonationStartResponse
{
    public Guid SessionId { get; init; }
    public Guid TargetCompanyId { get; init; }
    public Guid TargetUserId { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset ExpiresAtUtc { get; init; }
}

public sealed class PlatformSupportImpersonationEndRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class PlatformIncidentDto
{
    public string IncidentCode { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public DateTimeOffset DetectedAtUtc { get; init; }
}