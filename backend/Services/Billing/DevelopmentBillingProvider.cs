using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services.Billing;

public sealed class DevelopmentBillingProvider(IOptions<BillingOptions> options) : IBillingProvider
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    public string ProviderName => "Development";

    public Task<string> EnsureCustomerAsync(BillingCustomerRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult($"cus_dev_{request.CompanyId:N}"[..Math.Min(28, $"cus_dev_{request.CompanyId:N}".Length)]);
    }

    public Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult($"{options.Value.CheckoutSuccessUrl}?session_id=dev_{request.CompanyId:N}");
    }

    public Task<string> CreateBillingPortalSessionAsync(BillingPortalRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult($"{options.Value.BillingPortalReturnUrl}?portal=dev_{request.CompanyId:N}");
    }

    public Task<string> CreatePaymentMethodPortalSessionAsync(BillingPortalRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult($"{options.Value.BillingPortalReturnUrl}?portal=payment_method_update&customer={request.CustomerId}");
    }

    public Task<BillingProviderSubscriptionSnapshot> ChangeSubscriptionPlanAsync(ChangeSubscriptionPlanProviderRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(new BillingProviderSubscriptionSnapshot(
            request.SubscriptionId,
            "active",
            request.NewPriceId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1),
            null,
            false,
            DateTimeOffset.UtcNow));
    }

    public Task<BillingProviderSubscriptionSnapshot> CancelSubscriptionAsync(CancelSubscriptionProviderRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(new BillingProviderSubscriptionSnapshot(
            request.SubscriptionId,
            request.AtPeriodEnd ? "active" : "canceled",
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(request.AtPeriodEnd ? 14 : 0),
            null,
            request.AtPeriodEnd,
            DateTimeOffset.UtcNow));
    }

    public Task<BillingProviderSubscriptionSnapshot> ResumeSubscriptionAsync(ResumeSubscriptionProviderRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(new BillingProviderSubscriptionSnapshot(
            request.SubscriptionId,
            "active",
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1),
            null,
            false,
            DateTimeOffset.UtcNow));
    }

    public Task<BillingProviderSubscriptionSnapshot> GetSubscriptionSnapshotAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(new BillingProviderSubscriptionSnapshot(
            subscriptionId,
            "active",
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1),
            null,
            false,
            DateTimeOffset.UtcNow));
    }

    public BillingWebhookEnvelope ValidateAndParseWebhook(string rawBody, string signatureHeader)
    {
        _ = signatureHeader;
        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        var eventId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
        var eventType = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "unknown" : "unknown";
        var createdUnix = root.TryGetProperty("created", out var createdElement) && createdElement.ValueKind == JsonValueKind.Number
            ? createdElement.GetInt64()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dataObject = root.TryGetProperty("data", out var dataElement)
            && dataElement.TryGetProperty("object", out var objectElement)
            ? objectElement.Clone()
            : EmptyObject;

        var customerId = dataObject.ValueKind != JsonValueKind.Undefined && dataObject.TryGetProperty("customer", out var customerElement)
            ? customerElement.GetString()
            : null;
        var subscriptionId = dataObject.ValueKind != JsonValueKind.Undefined && dataObject.TryGetProperty("subscription", out var subscriptionElement)
            ? subscriptionElement.GetString()
            : null;

        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
        return new BillingWebhookEnvelope(
            eventId,
            eventType,
            DateTimeOffset.FromUnixTimeSeconds(createdUnix),
            customerId,
            subscriptionId,
            payloadHash,
            dataObject);
    }
}