namespace StayFlow.Api.Models;

public sealed class TenantSubscription : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public string Status { get; set; } = SubscriptionStatus.Active.ToStorageValue();
    public DateTimeOffset CurrentPeriodStartUtc { get; set; }
    public DateTimeOffset CurrentPeriodEndUtc { get; set; }
    public DateTimeOffset? TrialEndsAtUtc { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public string? ExternalSubscriptionId { get; set; }
    public string? ExternalPriceId { get; set; }
    public DateTimeOffset? LastProviderEventCreatedAtUtc { get; set; }
    public string? Notes { get; set; }

    public Company Company { get; set; } = null!;
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
}