namespace StayFlow.Api.DTOs.Billing;

public sealed class CreateCheckoutSessionRequest
{
    public string PlanName { get; init; } = string.Empty;
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