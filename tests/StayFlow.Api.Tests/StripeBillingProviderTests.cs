using System.Security.Cryptography;
using System.Text;
using System.Net;
using Microsoft.Extensions.Options;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Tests;

public sealed class StripeBillingProviderTests
{
    [Theory]
    [InlineData("""[{"id":"si_usage","price":{"id":"price_starter","recurring":{"usage_type":"metered"}}}]""")]
    [InlineData("""[{"id":"si_unknown","price":{"id":"price_unknown","recurring":{"usage_type":"licensed"}}}]""")]
    [InlineData("""[{"id":"si_a","price":{"id":"price_starter","recurring":{"usage_type":"licensed"}}},{"id":"si_b","price":{"id":"price_starter","recurring":{"usage_type":"licensed"}}}]""")]
    public void BaseItemSelection_FailsClosedForMeteredUnknownOrAmbiguousItems(string items)
    {
        using var subscription = System.Text.Json.JsonDocument.Parse("""{"items":{"data":""" + items + "}}");
        var options = new BillingOptions { PlanPriceIds = { ["Starter"] = "price_starter" } };
        Assert.Null(StripeBillingProvider.GetSubscriptionItemId(subscription, options));
        Assert.Null(StripeBillingProvider.GetBasePlanPriceId(subscription.RootElement, options));
    }

    [Theory]
    [InlineData("2024-06-20", true)]
    [InlineData("2026-09-30.endive", false)]
    [InlineData("", false)]
    public void BillingConfiguration_RequiresReviewedApiSchema(string version, bool valid)
    {
        var options = new BillingOptions
        {
            Provider = "Stripe",
            StripeSecretKey = "sk_test",
            StripeWebhookSigningSecret = "whsec_test",
            StripeApiVersion = version,
            PlanPriceIds = { ["Starter"] = "price_starter", ["Professional"] = "price_professional" }
        };
        Assert.Equal(valid, new BillingOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void Webhook_RejectsSignedPayloadFromUnreviewedApiVersion()
    {
        const string payload = """{"api_version":"2026-09-30.endive","id":"evt_wrong_schema","type":"invoice.paid","created":1730000000,"data":{"object":{"id":"in_1"}}}""";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = ComputeStripeSignature("whsec_test_value", timestamp, payload);
        var provider = CreateStripeProvider("whsec_test_value");

        var error = Assert.Throws<ForbiddenOperationException>(() =>
            provider.ValidateAndParseWebhook(payload, $"t={timestamp},v1={signature}"));

        Assert.Equal("stripe_webhook_api_version_mismatch", error.ErrorCode);
    }

    [Fact]
    public async Task ChangeSubscriptionPlan_UsesConfiguredBaseItemAndPinnedApiVersion()
    {
        using var handler = new SubscriptionChangeHandler();
        var options = new BillingOptions
        {
            StripeSecretKey = "sk_test_123",
            StripeApiVersion = "2024-06-20",
            PlanPriceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Starter"] = "price_starter",
                ["Professional"] = "price_professional"
            }
        };
        var provider = new StripeBillingProvider(
            new RecordingHttpClientFactory(handler),
            Options.Create(options));

        var result = await provider.ChangeSubscriptionPlanAsync(
            new ChangeSubscriptionPlanProviderRequest("sub_test", "price_professional", null),
            CancellationToken.None);

        Assert.Equal("price_professional", result.PriceId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal("2024-06-20", request.StripeVersion));
        Assert.Contains("items%5B0%5D%5Bid%5D=si_base", handler.Requests[1].Body);
        Assert.DoesNotContain("si_usage", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSubscriptionItemId_SelectsConfiguredBasePriceNotFirstItem()
    {
        using var subscription = System.Text.Json.JsonDocument.Parse(
            """
            {"items":{"data":[
              {"id":"si_usage","price":{"id":"price_usage"}},
              {"id":"si_base","price":{"id":"price_starter","recurring":{"usage_type":"licensed"}}}
            ]}}
            """);
        var options = new BillingOptions
        {
            PlanPriceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Starter"] = "price_starter",
                ["Professional"] = "price_professional"
            }
        };

        Assert.Equal("si_base", StripeBillingProvider.GetSubscriptionItemId(subscription, options));
    }

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

    private sealed class RecordingHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class SubscriptionChangeHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var apiVersion = request.Headers.TryGetValues("Stripe-Version", out var versions)
                ? versions.Single()
                : null;
            Requests.Add(new RecordedRequest(request.Method, body, apiVersion));
            var response = Requests.Count == 1
                ? """
                  {"id":"sub_test","status":"active","items":{"data":[
                    {"id":"si_usage","price":{"id":"price_usage"}},
                    {"id":"si_base","price":{"id":"price_starter","recurring":{"usage_type":"licensed"}}}
                  ]}}
                  """
                : """
                  {"id":"sub_test","status":"active","items":{"data":[
                    {"id":"si_usage","price":{"id":"price_usage"}},
                    {"id":"si_base","price":{"id":"price_professional","recurring":{"usage_type":"licensed"}}}
                  ]}}
                  """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response)
            };
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Body, string? StripeVersion);
}
