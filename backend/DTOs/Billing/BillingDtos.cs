namespace StayFlow.Api.DTOs.Billing;

public sealed class CreateCheckoutSessionRequest
{
    public string PlanName { get; init; } = string.Empty;
    public int? TrialDays { get; init; }
    public string? PaymentMethod { get; init; }
}

public sealed class CreateCheckoutSessionResponse
{
    public string CheckoutUrl { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
}

public sealed class CreateBillingPortalSessionResponse
{
    public string PortalUrl { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
}

public sealed class BillingSubscriptionResponse
{
    public Guid CompanyId { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool CancelAtPeriodEnd { get; init; }
    public DateTimeOffset CurrentPeriodStartUtc { get; init; }
    public DateTimeOffset CurrentPeriodEndUtc { get; init; }
    public DateTimeOffset? TrialEndsAtUtc { get; init; }
    public string? PlanName { get; init; }
    public string? ExternalSubscriptionId { get; init; }
    public string? ExternalPriceId { get; init; }
    public bool HasStripeCustomer { get; init; }
    public bool HasStripeSubscription { get; init; }
    public bool CanOpenBillingPortal { get; init; }
    public bool CanManagePaymentMethod { get; init; }
    public bool CanCancel { get; init; }
    public bool CanResume { get; init; }
    public bool CanStartCheckout { get; init; }
}

public sealed class BillingPlanResponse
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsEnterprise { get; init; }
    public bool IsCurrentPlan { get; init; }
    public string Currency { get; init; } = string.Empty;
    public long? MonthlyAmountMinor { get; init; }
    public int? TrialDays { get; init; }
    public long? PropertyLimit { get; init; }
    public long? TeamLimit { get; init; }
    public long? AiRequestLimit { get; init; }
    public long? WhatsAppMessageLimit { get; init; }
}

public sealed class BillingPaymentOptionResponse
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class ChangeSubscriptionPlanRequest
{
    public string PlanName { get; init; } = string.Empty;
}

public sealed class CancelSubscriptionRequest
{
    public bool AtPeriodEnd { get; init; } = true;
}

public sealed class UsageSummaryResponse
{
    public Guid CompanyId { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public IReadOnlyCollection<UsageMetricSummaryDto> Metrics { get; init; } = [];
}

public sealed class UsageMetricSummaryDto
{
    public string Metric { get; init; } = string.Empty;
    public string EntitlementKey { get; init; } = string.Empty;
    public long Used { get; init; }
    public long? Limit { get; init; }
    public long? Remaining { get; init; }
    public bool IsUnlimited { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTimeOffset PeriodStartUtc { get; init; }
    public DateTimeOffset PeriodEndUtc { get; init; }
}

public sealed class TenantInvoiceDto
{
    public Guid Id { get; init; }
    public string ExternalInvoiceId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long AmountDue { get; init; }
    public long AmountPaid { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTimeOffset? PeriodStartUtc { get; init; }
    public DateTimeOffset? PeriodEndUtc { get; init; }
    public DateTimeOffset? PaidAtUtc { get; init; }
    public DateTimeOffset? FailedAtUtc { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class BillingWebhookProcessingResult
{
    public string EventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public bool WasDuplicate { get; init; }
    public bool AppliedStateChange { get; init; }
}