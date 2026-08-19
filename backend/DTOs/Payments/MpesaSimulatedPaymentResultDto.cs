namespace StayFlow.Api.DTOs.Payments;

/// <summary>Result surface for the development-only M-PESA success simulator.</summary>
public sealed class MpesaSimulatedPaymentResultDto
{
    public Guid PaymentId { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? ProviderTransactionId { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }
}
