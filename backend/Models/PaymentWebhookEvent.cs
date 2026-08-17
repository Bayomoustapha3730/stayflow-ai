namespace StayFlow.Api.Models;

/// <summary>
/// Tracks payment provider webhook events for idempotency.
/// Prevents duplicate processing of provider callbacks (e.g., M-PESA retries).
/// Similar to BillingWebhookEvent but for guest payments.
/// </summary>
public sealed class PaymentWebhookEvent : AuditableEntity
{
    /// <summary>
    /// Payment provider name (e.g., "M-PESA", "Stripe").
    /// </summary>
    public string Provider { get; set; } = "M-PESA";

    /// <summary>
    /// Unique event ID from provider (e.g., M-PESA MerchantRequestID).
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Callback event type from provider (e.g., "STKPushCallback").
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// M-PESA CheckoutRequestID for correlating to PaymentTransaction.
    /// </summary>
    public string? CheckoutRequestId { get; set; }

    /// <summary>
    /// M-PESA transaction ID / receipt number.
    /// </summary>
    public string? TransactionId { get; set; }

    /// <summary>
    /// Timestamp of event creation at provider.
    /// </summary>
    public DateTimeOffset EventCreatedAtUtc { get; set; }

    /// <summary>
    /// Hash of raw webhook payload for duplicate detection.
    /// </summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when event was processed.
    /// </summary>
    public DateTimeOffset ProcessedAtUtc { get; set; }

    /// <summary>
    /// Whether this was a duplicate (already processed earlier).
    /// </summary>
    public bool WasDuplicate { get; set; }
}
