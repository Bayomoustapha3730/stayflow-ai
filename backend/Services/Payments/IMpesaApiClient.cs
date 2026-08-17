namespace StayFlow.Api.Services.Payments;

public sealed record MpesaStkPushRequest(
    string BusinessShortCode,
    string Password,
    string Timestamp,
    string TransactionType,
    decimal Amount,
    string PartyA,
    string PartyB,
    string PhoneNumber,
    string CallbackUrl,
    string AccountReference,
    string TransactionDescription);

public sealed record MpesaStkPushResponse(
    string? MerchantRequestId,
    string? CheckoutRequestId,
    int ResponseCode,
    string ResponseDescription,
    string? CustomerMessage);

public interface IMpesaApiClient
{
    Task<MpesaStkPushResponse> InitiateStkPushAsync(
        MpesaStkPushRequest request,
        CancellationToken cancellationToken);
}
