namespace StayFlow.Api.Models;

/// <summary>
/// Guest/reservation payment transaction.
/// Separate from subscription billing (TenantSubscription).
/// Supports multiple payment providers: M-PESA, Stripe, Flutterwave, etc.
/// </summary>
public sealed class Payment : AuditableEntity
{
    // Tenant scoping
    public Guid CompanyId { get; set; }

    // Business context
    public Guid PropertyId { get; set; }
    public Guid GuestId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? ServiceRequestId { get; set; }

    // Payment amount and currency (KES for Kenya M-PESA)
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KES";

    // Provider identification
    public string Provider { get; set; } = "M-PESA";
    public string ProviderEnvironment { get; set; } = "Sandbox";
    public string PaymentMethod { get; set; } = "STKPush";

    // Provider transaction identifiers (for callback correlation + idempotency)
    public string? ProviderRequestId { get; set; }
    public string? ProviderCheckoutRequestId { get; set; }
    public string? ProviderTransactionId { get; set; }

    // Customer contact
    public string? CustomerPhoneNumber { get; set; }

    // Payment metadata
    public string? ExternalReference { get; set; }
    public string? InternalReference { get; set; }
    public string Status { get; set; } = "Pending";

    // Failure details (sanitized; never expose provider secrets)
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }

    // Lifecycle timestamps
    public DateTimeOffset? RequestedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? FailedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Guest Guest { get; set; } = null!;
    public Reservation? Reservation { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }
}
