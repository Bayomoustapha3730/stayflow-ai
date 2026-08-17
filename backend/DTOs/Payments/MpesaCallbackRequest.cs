using System.Text.Json.Serialization;

namespace StayFlow.Api.DTOs.Payments;

/// <summary>
/// Raw Safaricom Daraja STK Push callback envelope.
/// Shape defined by Safaricom; do not trust any tenant/company identifier from this payload.
/// </summary>
public sealed class MpesaStkCallbackEnvelope
{
    [JsonPropertyName("Body")]
    public MpesaStkCallbackBody? Body { get; init; }
}

public sealed class MpesaStkCallbackBody
{
    [JsonPropertyName("stkCallback")]
    public MpesaStkCallback? StkCallback { get; init; }
}

public sealed class MpesaStkCallback
{
    [JsonPropertyName("MerchantRequestID")]
    public string? MerchantRequestId { get; init; }

    [JsonPropertyName("CheckoutRequestID")]
    public string? CheckoutRequestId { get; init; }

    /// <summary>0 indicates success; any non-zero value is a provider failure/cancellation code.</summary>
    [JsonPropertyName("ResultCode")]
    public int ResultCode { get; init; }

    [JsonPropertyName("ResultDesc")]
    public string? ResultDesc { get; init; }

    [JsonPropertyName("CallbackMetadata")]
    public MpesaCallbackMetadata? CallbackMetadata { get; init; }
}

public sealed class MpesaCallbackMetadata
{
    [JsonPropertyName("Item")]
    public IReadOnlyList<MpesaCallbackMetadataItem> Item { get; init; } = [];
}

public sealed class MpesaCallbackMetadataItem
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Safaricom sends heterogeneous value types (decimal, string, long); deserialize as object.</summary>
    [JsonPropertyName("Value")]
    public object? Value { get; init; }
}
