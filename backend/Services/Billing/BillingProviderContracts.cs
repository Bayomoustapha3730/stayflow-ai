using System.Text.Json;

namespace StayFlow.Api.Services.Billing;

public interface IBillingProvider
{
    string ProviderName { get; }

    Task<string> EnsureCustomerAsync(BillingCustomerRequest request, CancellationToken cancellationToken);
    Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken);
    Task<string> CreateBillingPortalSessionAsync(BillingPortalRequest request, CancellationToken cancellationToken);
    BillingWebhookEnvelope ValidateAndParseWebhook(string rawBody, string signatureHeader);
}

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    public string Provider { get; set; } = "Development";
    public string StripeSecretKey { get; set; } = string.Empty;
    public string StripeWebhookSigningSecret { get; set; } = string.Empty;
    public string CheckoutSuccessUrl { get; set; } = "https://example.invalid/success";
    public string CheckoutCancelUrl { get; set; } = "https://example.invalid/cancel";
    public string BillingPortalReturnUrl { get; set; } = "https://example.invalid/billing";
    public Dictionary<string, string> PlanPriceIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int WebhookToleranceSeconds { get; set; } = 300;
    public int WebhookMaxBodyBytes { get; set; } = 262144;
}

public sealed record BillingCustomerRequest(Guid CompanyId, string CompanyName, string Email);

public sealed record CheckoutSessionRequest(
    Guid CompanyId,
    string CustomerId,
    string PriceId,
    string SuccessUrl,
    string CancelUrl,
    string? CorrelationId);

public sealed record BillingPortalRequest(
    Guid CompanyId,
    string CustomerId,
    string ReturnUrl,
    string? CorrelationId);

public sealed record BillingWebhookEnvelope(
    string EventId,
    string EventType,
    DateTimeOffset EventCreatedAtUtc,
    string? CustomerId,
    string? SubscriptionId,
    string PayloadHash,
    JsonElement DataObject);