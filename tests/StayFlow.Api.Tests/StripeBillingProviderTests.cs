using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Tests;

public sealed class StripeBillingProviderTests
{
    [Fact]
    public void ValidateAndParseWebhook_WithValidSignature_ParsesDurablePayload()
    {
        var provider = CreateStripeProvider("whsec_test_value");
        var payload = "{" +
            "\"id\":\"evt_1\"," +
            "\"type\":\"invoice.paid\"," +
            "\"created\":1730000000," +
            "\"data\":{\"object\":{" +
            "\"id\":\"in_1\"," +
            "\"customer\":\"cus_1\"," +
            "\"subscription\":\"sub_1\"}}}";

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = ComputeStripeSignature("whsec_test_value", timestamp, payload);

        var envelope = provider.ValidateAndParseWebhook(payload, $"t={timestamp},v1={signature}");

        Assert.Equal("evt_1", envelope.EventId);
        Assert.Equal("invoice.paid", envelope.EventType);
        Assert.Equal("cus_1", envelope.CustomerId);
        Assert.Equal("sub_1", envelope.SubscriptionId);
        Assert.Equal("in_1", envelope.DataObject.GetProperty("id").GetString());
    }

    [Fact]
    public void ValidateAndParseWebhook_WithInvalidSignature_ThrowsForbidden()
    {
        var provider = CreateStripeProvider("whsec_test_value");
        var payload = "{" +
            "\"id\":\"evt_2\"," +
            "\"type\":\"invoice.paid\"," +
            "\"created\":1730000000," +
            "\"data\":{\"object\":{\"id\":\"in_2\"}}}";

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Assert.Throws<ForbiddenOperationException>(() =>
            provider.ValidateAndParseWebhook(payload, $"t={timestamp},v1=bad_signature"));
    }

    [Fact]
    public void DevelopmentProvider_ValidateAndParseWebhook_ParsesDurablePayload()
    {
        var options = Options.Create(new BillingOptions());
        var provider = new DevelopmentBillingProvider(options);
        var payload = "{" +
            "\"id\":\"evt_dev_1\"," +
            "\"type\":\"customer.subscription.updated\"," +
            "\"created\":1730000000," +
            "\"data\":{\"object\":{" +
            "\"id\":\"sub_dev_1\"," +
            "\"customer\":\"cus_dev_1\"," +
            "\"subscription\":\"sub_dev_1\"}}}";

        var envelope = provider.ValidateAndParseWebhook(payload, "ignored");

        Assert.Equal("evt_dev_1", envelope.EventId);
        Assert.Equal("customer.subscription.updated", envelope.EventType);
        Assert.Equal("cus_dev_1", envelope.CustomerId);
        Assert.Equal("sub_dev_1", envelope.SubscriptionId);
        Assert.Equal("sub_dev_1", envelope.DataObject.GetProperty("id").GetString());
    }

    private static StripeBillingProvider CreateStripeProvider(string secret)
    {
        var options = Options.Create(new BillingOptions
        {
            StripeWebhookSigningSecret = secret,
            StripeSecretKey = "sk_test_123"
        });

        return new StripeBillingProvider(new NoOpHttpClientFactory(), options);
    }

    private static string ComputeStripeSignature(string secret, long timestamp, string rawBody)
    {
        var payload = $"{timestamp}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class NoOpHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
