namespace StayFlow.Api.DTOs.Payments;

/// <summary>
/// Host-facing, tenant-scoped view of a guest/reservation payment. Never includes provider secrets.
/// </summary>
public sealed class PaymentDto
{
    public Guid Id { get; init; }
    public Guid? ReservationId { get; init; }
    public Guid PropertyId { get; init; }
    public Guid GuestId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "KES";
    public string Provider { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ProviderTransactionId { get; init; }
    public string? CustomerPhoneNumber { get; init; }
    public string? InternalReference { get; init; }
    public string? FailureMessage { get; init; }
    public DateTimeOffset? RequestedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset? FailedAtUtc { get; init; }
    public DateTimeOffset? CancelledAtUtc { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
