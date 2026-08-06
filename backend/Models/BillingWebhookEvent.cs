namespace StayFlow.Api.Models;

public sealed class BillingWebhookEvent : AuditableEntity
{
    public string Provider { get; set; } = "Stripe";
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public DateTimeOffset EventCreatedAtUtc { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAtUtc { get; set; }
    public bool WasDuplicate { get; set; }
}