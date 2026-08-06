namespace StayFlow.Api.Models;

public sealed class PlanEntitlement : AuditableEntity
{
    public Guid SubscriptionPlanId { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public long? QuotaLimit { get; set; }
    public bool IsUnlimited { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }

    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
}