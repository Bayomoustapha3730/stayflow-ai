using System.Text.Json.Serialization;

namespace StayFlow.Api.Services.Payments;

public sealed record MpesaStkPushRequest(
    [property: JsonPropertyName("BusinessShortCode")]
    string BusinessShortCode,

    [property: JsonPropertyName("Password")]
    string Password,

    [property: JsonPropertyName("Timestamp")]
    string Timestamp,

    [property: JsonPropertyName("TransactionType")]
    string TransactionType,

    [property: JsonPropertyName("Amount")]
    decimal Amount,

    [property: JsonPropertyName("PartyA")]
    string PartyA,

    [property: JsonPropertyName("PartyB")]
    string PartyB,

    [property: JsonPropertyName("PhoneNumber")]
    string PhoneNumber,

    [property: JsonPropertyName("CallBackURL")]
    string CallbackUrl,

    [property: JsonPropertyName("AccountReference")]
    string AccountReference,

    [property: JsonPropertyName("TransactionDesc")]
    string TransactionDescription);

public sealed record MpesaStkPushResponse(
    [property: JsonPropertyName("MerchantRequestID")]
    string? MerchantRequestId,

    [property: JsonPropertyName("CheckoutRequestID")]
    string? CheckoutRequestId,

    [property: JsonPropertyName("ResponseCode")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int ResponseCode,

    [property: JsonPropertyName("ResponseDescription")]
    string ResponseDescription,

    [property: JsonPropertyName("CustomerMessage")]
    string? CustomerMessage);

public sealed record MpesaStkQueryRequest(
    [property: JsonPropertyName("BusinessShortCode")]
    string BusinessShortCode,

    [property: JsonPropertyName("Password")]
    string Password,

    [property: JsonPropertyName("Timestamp")]
    string Timestamp,

    [property: JsonPropertyName("CheckoutRequestID")]
    string CheckoutRequestId);

public sealed record MpesaStkQueryResponse(
    [property: JsonPropertyName("ResponseCode")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int ResponseCode,

    [property: JsonPropertyName("ResponseDescription")]
    string? ResponseDescription,

    [property: JsonPropertyName("MerchantRequestID")]
    string? MerchantRequestId,

    [property: JsonPropertyName("CheckoutRequestID")]
    string? CheckoutRequestId,

    [property: JsonPropertyName("ResultCode")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int? ResultCode,

    [property: JsonPropertyName("ResultDesc")]
    string? ResultDescription);

public interface IMpesaApiClient
{
    Task<MpesaStkPushResponse> InitiateStkPushAsync(
        MpesaStkPushRequest request,
        CancellationToken cancellationToken);

    Task<MpesaStkQueryResponse> QueryStkPushAsync(
        MpesaStkQueryRequest request,
        CancellationToken cancellationToken);
}
