namespace StayFlow.Api.DTOs.Payments;

/// <summary>
/// M-PESA integration health status. Never includes credential values.
/// </summary>
public sealed class MpesaHealthResponse
{
    public string Status { get; init; } = "Disabled";
    public string Message { get; init; } = string.Empty;
    public bool IsOperational { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
}
