using System.Text.Json;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Tests;

public sealed class MpesaDarajaContractTests
{
    [Fact]
    public void StkPushRequest_SerializesUsingExactDarajaPropertyNames()
    {
        var request = new MpesaStkPushRequest(
            BusinessShortCode: "174379",
            Password: "password",
            Timestamp: "20260817180000",
            TransactionType: "CustomerPayBillOnline",
            Amount: 1m,
            PartyA: "254712345678",
            PartyB: "174379",
            PhoneNumber: "254712345678",
            CallbackUrl: "https://example.test/webhooks/mpesa/stk",
            AccountReference: "DEMO-CONF-001",
            TransactionDescription: "StayFlow sandbox test");

        var json = JsonSerializer.Serialize(request);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("174379", root.GetProperty("BusinessShortCode").GetString());
        Assert.Equal("password", root.GetProperty("Password").GetString());
        Assert.Equal("20260817180000", root.GetProperty("Timestamp").GetString());
        Assert.Equal("CustomerPayBillOnline", root.GetProperty("TransactionType").GetString());
        Assert.Equal(1m, root.GetProperty("Amount").GetDecimal());
        Assert.Equal("254712345678", root.GetProperty("PartyA").GetString());
        Assert.Equal("174379", root.GetProperty("PartyB").GetString());
        Assert.Equal("254712345678", root.GetProperty("PhoneNumber").GetString());
        Assert.Equal(
            "https://example.test/webhooks/mpesa/stk",
            root.GetProperty("CallBackURL").GetString());
        Assert.Equal("DEMO-CONF-001", root.GetProperty("AccountReference").GetString());
        Assert.Equal(
            "StayFlow sandbox test",
            root.GetProperty("TransactionDesc").GetString());

        Assert.False(root.TryGetProperty("businessShortCode", out _));
        Assert.False(root.TryGetProperty("callbackUrl", out _));
        Assert.False(root.TryGetProperty("CallbackUrl", out _));
        Assert.False(root.TryGetProperty("transactionDescription", out _));
        Assert.False(root.TryGetProperty("TransactionDescription", out _));
    }

    [Fact]
    public void StkPushResponse_DeserializesDarajaStringResponseCode()
    {
        const string json = """
        {
          "MerchantRequestID": "merchant-123",
          "CheckoutRequestID": "checkout-123",
          "ResponseCode": "0",
          "ResponseDescription": "Success. Request accepted for processing",
          "CustomerMessage": "Success. Request accepted for processing"
        }
        """;

        var response = JsonSerializer.Deserialize<MpesaStkPushResponse>(json);

        Assert.NotNull(response);
        Assert.Equal("merchant-123", response.MerchantRequestId);
        Assert.Equal("checkout-123", response.CheckoutRequestId);
        Assert.Equal(0, response.ResponseCode);
        Assert.Equal(
            "Success. Request accepted for processing",
            response.ResponseDescription);
        Assert.Equal(
            "Success. Request accepted for processing",
            response.CustomerMessage);
    }
}
