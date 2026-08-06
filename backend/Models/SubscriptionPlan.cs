namespace StayFlow.Api.Models;

public sealed class SubscriptionPlan : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsEnterprise { get; set; }
    public int SortOrder { get; set; }

    public ICollection<PlanEntitlement> Entitlements { get; set; } = [];
    public ICollection<TenantSubscription> TenantSubscriptions { get; set; } = [];
}