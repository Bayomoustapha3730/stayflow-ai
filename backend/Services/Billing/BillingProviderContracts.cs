using System.Text.Json;
using Microsoft.Extensions.Options;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.Billing;

public interface IBillingProvider
{
    string ProviderName { get; }

    Task<string> EnsureCustomerAsync(BillingCustomerRequest request, CancellationToken cancellationToken);
    Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken);
    Task<string> CreateBillingPortalSessionAsync(BillingPortalRequest request, CancellationToken cancellationToken);
    Task<string> CreatePaymentMethodPortalSessionAsync(BillingPortalRequest request, CancellationToken cancellationToken);
    Task<BillingProviderSubscriptionSnapshot> ChangeSubscriptionPlanAsync(ChangeSubscriptionPlanProviderRequest request, CancellationToken cancellationToken);
    Task<BillingProviderSubscriptionSnapshot> CancelSubscriptionAsync(CancelSubscriptionProviderRequest request, CancellationToken cancellationToken);
    Task<BillingProviderSubscriptionSnapshot> ResumeSubscriptionAsync(ResumeSubscriptionProviderRequest request, CancellationToken cancellationToken);
    Task<BillingProviderSubscriptionSnapshot> GetSubscriptionSnapshotAsync(string subscriptionId, CancellationToken cancellationToken);
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
    public Dictionary<string, long> PlanMonthlyAmountsMinor { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PlanCurrencies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> PlanTrialDays { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> CountryCurrencies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string[]> CountryPaymentMethods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string[] DefaultPaymentMethods { get; set; } = ["Card"];
    public string DefaultCurrency { get; set; } = "USD";
    public int WebhookToleranceSeconds { get; set; } = 300;
    public int WebhookMaxBodyBytes { get; set; } = 262144;
}

public sealed class BillingOptionsValidator : IValidateOptions<BillingOptions>
{
    public ValidateOptionsResult Validate(string? name, BillingOptions options)
    {
        if (!string.Equals(options.Provider, "Stripe", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.StripeSecretKey))
        {
            errors.Add("Billing:StripeSecretKey is required when Billing:Provider is Stripe.");
        }

        if (string.IsNullOrWhiteSpace(options.StripeWebhookSigningSecret))
        {
            errors.Add("Billing:StripeWebhookSigningSecret is required when Billing:Provider is Stripe.");
        }

        foreach (var planName in new[] { SubscriptionPlanNames.Starter, SubscriptionPlanNames.Professional })
        {
            if (!options.PlanPriceIds.TryGetValue(planName, out var priceId) || string.IsNullOrWhiteSpace(priceId))
            {
                errors.Add($"Billing:PlanPriceIds:{planName} is required when Billing:Provider is Stripe.");
            }
        }

        if (options.PlanPriceIds.Keys.Any(key => string.Equals(key, "Growth", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "Scale", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("Billing:PlanPriceIds must use canonical plan names; Growth and Scale are not valid plan identities.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public sealed record BillingCustomerRequest(Guid CompanyId, string CompanyName, string Email);

public sealed record CheckoutSessionRequest(
    Guid CompanyId,
    string CustomerId,
    string PriceId,
    string SuccessUrl,
    string CancelUrl,
    string? CorrelationId,
    int? TrialDays);

public sealed record BillingPortalRequest(
    Guid CompanyId,
    string CustomerId,
    string ReturnUrl,
    string? CorrelationId);

public sealed record ChangeSubscriptionPlanProviderRequest(
    string SubscriptionId,
    string NewPriceId,
    string? CorrelationId);

public sealed record CancelSubscriptionProviderRequest(
    string SubscriptionId,
    bool AtPeriodEnd,
    string? CorrelationId);

public sealed record ResumeSubscriptionProviderRequest(
    string SubscriptionId,
    string? CorrelationId);

public sealed record BillingProviderSubscriptionSnapshot(
    string SubscriptionId,
    string Status,
    string? PriceId,
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    DateTimeOffset? TrialEndsAtUtc,
    bool CancelAtPeriodEnd,
    DateTimeOffset EventCreatedAtUtc);

public sealed record BillingWebhookEnvelope(
    string EventId,
    string EventType,
    DateTimeOffset EventCreatedAtUtc,
    string? CustomerId,
    string? SubscriptionId,
    string PayloadHash,
    JsonElement DataObject);