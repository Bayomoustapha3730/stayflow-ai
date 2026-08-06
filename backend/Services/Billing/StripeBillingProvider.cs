using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StayFlow.Api.Exceptions;

namespace StayFlow.Api.Services.Billing;

public sealed class StripeBillingProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<BillingOptions> options) : IBillingProvider
{
    private const string StripeApiBase = "https://api.stripe.com/v1";

    public string ProviderName => "Stripe";

    public async Task<string> EnsureCustomerAsync(BillingCustomerRequest request, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["name"] = request.CompanyName,
            ["email"] = request.Email,
            ["metadata[company_id]"] = request.CompanyId.ToString("D")
        };

        var document = await PostFormAsync("customers", form, cancellationToken);
        var id = document.RootElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ExternalDependencyException("Stripe customer creation failed.", "stripe_customer_create_failed");
        }

        return id;
    }

    public async Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["mode"] = "subscription",
            ["customer"] = request.CustomerId,
            ["success_url"] = request.SuccessUrl,
            ["cancel_url"] = request.CancelUrl,
            ["line_items[0][price]"] = request.PriceId,
            ["line_items[0][quantity]"] = "1",
            ["metadata[company_id]"] = request.CompanyId.ToString("D")
        };

        var document = await PostFormAsync("checkout/sessions", form, cancellationToken);
        var url = document.RootElement.TryGetProperty("url", out var element) ? element.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ExternalDependencyException("Stripe checkout session creation failed.", "stripe_checkout_failed");
        }

        return url;
    }

    public async Task<string> CreateBillingPortalSessionAsync(BillingPortalRequest request, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["customer"] = request.CustomerId,
            ["return_url"] = request.ReturnUrl
        };

        var document = await PostFormAsync("billing_portal/sessions", form, cancellationToken);
        var url = document.RootElement.TryGetProperty("url", out var element) ? element.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ExternalDependencyException("Stripe billing portal session creation failed.", "stripe_portal_failed");
        }

        return url;
    }

    public BillingWebhookEnvelope ValidateAndParseWebhook(string rawBody, string signatureHeader)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.StripeWebhookSigningSecret))
        {
            throw new ForbiddenOperationException("Stripe webhook signing secret is not configured.", "stripe_webhook_secret_missing");
        }

        if (!TryParseStripeSignature(signatureHeader, out var timestamp, out var signatures))
        {
            throw new ForbiddenOperationException("Stripe signature header is invalid.", "stripe_signature_invalid");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > config.WebhookToleranceSeconds)
        {
            throw new ForbiddenOperationException("Stripe webhook timestamp is outside tolerance.", "stripe_signature_expired");
        }

        var signedPayload = $"{timestamp}.{rawBody}";
        var computed = ComputeHmac(config.StripeWebhookSigningSecret, signedPayload);
        var anyMatch = signatures.Any(signature => FixedEquals(computed, signature));
        if (!anyMatch)
        {
            throw new ForbiddenOperationException("Stripe signature verification failed.", "stripe_signature_invalid");
        }

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        var eventId = root.GetProperty("id").GetString() ?? string.Empty;
        var eventType = root.GetProperty("type").GetString() ?? string.Empty;
        var created = root.GetProperty("created").GetInt64();
        var dataObject = root.GetProperty("data").GetProperty("object");

        string? customerId = null;
        string? subscriptionId = null;

        if (dataObject.TryGetProperty("customer", out var customerElement) && customerElement.ValueKind == JsonValueKind.String)
        {
            customerId = customerElement.GetString();
        }

        if (dataObject.TryGetProperty("subscription", out var subscriptionElement))
        {
            subscriptionId = subscriptionElement.ValueKind == JsonValueKind.String
                ? subscriptionElement.GetString()
                : null;
        }

        if (string.IsNullOrWhiteSpace(subscriptionId) && dataObject.TryGetProperty("id", out var idElement))
        {
            var id = idElement.GetString();
            if (eventType.StartsWith("customer.subscription", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(id))
            {
                subscriptionId = id;
            }
        }

        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
        var clonedObject = JsonDocument.Parse(dataObject.GetRawText()).RootElement;
        return new BillingWebhookEnvelope(
            eventId,
            eventType,
            DateTimeOffset.FromUnixTimeSeconds(created),
            customerId,
            subscriptionId,
            payloadHash,
            clonedObject);
    }

    private async Task<JsonDocument> PostFormAsync(string path, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken)
    {
        var key = options.Value.StripeSecretKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ExternalDependencyException("Stripe secret key is not configured.", "stripe_secret_missing");
        }

        var client = httpClientFactory.CreateClient(nameof(StripeBillingProvider));
        client.BaseAddress = new Uri(StripeApiBase + "/", UriKind.Absolute);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(path, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalDependencyException("Stripe request failed.", "stripe_http_error");
        }

        return JsonDocument.Parse(body);
    }

    private static bool TryParseStripeSignature(string header, out long timestamp, out List<string> signatures)
    {
        timestamp = 0;
        signatures = [];

        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var parts = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2)
            {
                continue;
            }

            if (string.Equals(kv[0], "t", StringComparison.OrdinalIgnoreCase) && long.TryParse(kv[1], out var parsed))
            {
                timestamp = parsed;
            }

            if (string.Equals(kv[0], "v1", StringComparison.OrdinalIgnoreCase))
            {
                signatures.Add(kv[1]);
            }
        }

        return timestamp > 0 && signatures.Count > 0;
    }

    private static string ComputeHmac(string secret, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}