namespace StayFlow.Api.Models;

/// <summary>
/// Payment transaction status lifecycle for guest/reservation payments.
/// NOT used for subscription billing—see SubscriptionStatus for billing domain.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Payment has been initiated but not yet processed by provider.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Payment is being processed (e.g., STK push sent, awaiting customer approval).
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Payment has been successfully completed.
    /// Terminal state—no transitions away.
    /// </summary>
    Paid = 2,

    /// <summary>
    /// Payment was rejected by provider or customer.
    /// Terminal state—no transitions away without refund implementation.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Payment was cancelled by customer or system.
    /// Terminal state.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Payment request expired (e.g., STK push timeout).
    /// May transition to Failed if no customer response.
    /// </summary>
    Expired = 5
}

public static class PaymentStatusExtensions
{
    /// <summary>
    /// Convert enum to string for storage.
    /// </summary>
    public static string ToStorageValue(this PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Pending",
        PaymentStatus.Processing => "Processing",
        PaymentStatus.Paid => "Paid",
        PaymentStatus.Failed => "Failed",
        PaymentStatus.Cancelled => "Cancelled",
        PaymentStatus.Expired => "Expired",
        _ => "Pending"
    };

    /// <summary>
    /// Parse string to enum.
    /// </summary>
    public static PaymentStatus ToPaymentStatus(this string value) =>
        value.Trim() switch
        {
            "Pending" => PaymentStatus.Pending,
            "Processing" => PaymentStatus.Processing,
            "Paid" => PaymentStatus.Paid,
            "Failed" => PaymentStatus.Failed,
            "Cancelled" => PaymentStatus.Cancelled,
            "Expired" => PaymentStatus.Expired,
            _ => PaymentStatus.Pending
        };

    /// <summary>
    /// Check if status is terminal (no further transitions possible).
    /// </summary>
    public static bool IsTerminal(this PaymentStatus status) => status is 
        PaymentStatus.Paid or 
        PaymentStatus.Failed or 
        PaymentStatus.Cancelled;
}
