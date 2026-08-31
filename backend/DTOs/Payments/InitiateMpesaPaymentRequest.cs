using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace StayFlow.Api.DTOs.Payments;

public sealed class InitiateMpesaPaymentRequest
{
    [Required]
    public Guid ReservationId { get; init; }

    [Required, StringLength(32)]
    public string CustomerPhoneNumber { get; init; } = string.Empty;

    [JsonIgnore]
    public decimal? AmountOverride { get; init; }

    [StringLength(160)]
    public string? Description { get; init; }

    [StringLength(100)]
    public string? IdempotencyKey { get; init; }
}
