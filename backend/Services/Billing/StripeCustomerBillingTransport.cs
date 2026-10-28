using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services.Billing;

public sealed class StripeCustomerBillingTransport(
    IHttpClientFactory httpClientFactory,
    IOptions<BillingOptions> billingOptions,
    IOptions<CustomerUsageBillingOptions> usageBillingOptions,
    ApplicationDbContext dbContext,
    TimeProvider timeProvider) : ICustomerBillingExternalTransport
{
    private const string StripeApiBase = "https://api.stripe.com/v1/";
    private static readonly TimeSpan MaximumEventAge = TimeSpan.FromDays(35);
    private static readonly TimeSpan MaximumFutureEventTime = TimeSpan.FromMinutes(5);

    public async Task<CustomerBillingExternalResult> SendAsync(
        CustomerBillingExternalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var usageOptions = usageBillingOptions.Value;
        if (!usageOptions.Enabled)
        {
            return Result(CustomerBillingExternalTransportOutcome.Disabled, "usage_billing_disabled");
        }

        if (!string.Equals(billingOptions.Value.Provider, "Stripe", StringComparison.OrdinalIgnoreCase))
        {
            return Result(CustomerBillingExternalTransportOutcome.Disabled, "stripe_billing_provider_not_selected");
        }

        var mappingResult = ResolveMapping(request, usageOptions);
        if (mappingResult.Error is not null)
        {
            return mappingResult.Error;
        }

        if (request.DeliveryId == Guid.Empty
            || request.CompanyId == Guid.Empty
            || request.TenantSubscriptionId == Guid.Empty
            || request.SubscriptionPlanId == Guid.Empty
            || request.BillableQuantity < 0
            || request.ChargeAmount < 0
            || request.Currency != "USD"
            || string.IsNullOrWhiteSpace(request.ExternalIdempotencyKey)
            || request.ExternalIdempotencyKey.Length > 100
            || request.PeriodStartUtc.Offset != TimeSpan.Zero
            || request.PeriodEndUtc.Offset != TimeSpan.Zero
            || request.PeriodStartUtc >= request.PeriodEndUtc)
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "invalid_delivery_snapshot");
        }

        var now = timeProvider.GetUtcNow();
        if (request.PeriodStartUtc < now - MaximumEventAge
            || request.PeriodStartUtc > now + MaximumFutureEventTime)
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "usage_period_outside_stripe_timestamp_window");
        }

        var delivery = await dbContext.CustomerBillingDeliveries
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.DeliveryId, cancellationToken);
        if (delivery is null)
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "delivery_not_found");
        }

        if (delivery.DeliveryState != CustomerBillingDeliveryState.Dispatching
            || delivery.DeliveryProvider != CustomerBillingDeliveryProvider.Stripe
            || delivery.DeliveryKind != CustomerBillingDeliveryKind.PendingExternalBilling)
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "delivery_not_claimed");
        }

        if (delivery.CompanyId != request.CompanyId
            || delivery.TenantSubscriptionId != request.TenantSubscriptionId
            || delivery.SubscriptionPlanId != request.SubscriptionPlanId
            || delivery.Metric != request.Metric
            || delivery.BillableQuantitySnapshot != request.BillableQuantity
            || delivery.ChargeAmountSnapshot != request.ChargeAmount
            || delivery.CurrencySnapshot != request.Currency
            || delivery.PeriodStartUtc != request.PeriodStartUtc
            || delivery.PeriodEndUtc != request.PeriodEndUtc
            || delivery.ExternalIdempotencyKey != request.ExternalIdempotencyKey
            || delivery.BillingStateSnapshot != CustomerBillableUsageState.Calculated
            || delivery.BillingReasonSnapshot != CustomerBillableUsageReason.Calculated)
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "delivery_snapshot_mismatch");
        }

        var subscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.TenantSubscriptionId, cancellationToken);
        if (subscription is null)
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "subscription_mapping_missing");
        }

        if (subscription.CompanyId != request.CompanyId
            || subscription.SubscriptionPlanId != request.SubscriptionPlanId)
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "subscription_mapping_mismatch");
        }

        if (string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId))
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "stripe_subscription_mapping_missing");
        }

        if (subscription.Status is not ("Active" or "Trialing"))
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "subscription_not_eligible");
        }

        var stripeCustomerId = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.Id == request.CompanyId)
            .Select(company => company.StripeCustomerId)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "stripe_customer_mapping_missing");
        }

        var secretKey = billingOptions.Value.StripeSecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return Result(CustomerBillingExternalTransportOutcome.ConfigurationError, "stripe_secret_missing");
        }

        var mapping = mappingResult.Mapping!;
        var payload = new Dictionary<string, string>
        {
            ["event_name"] = mapping.MeterEventName,
            [$"payload[{mapping.CustomerPayloadKey}]"] = stripeCustomerId,
            [$"payload[{mapping.ValuePayloadKey}]"] = request.BillableQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["timestamp"] = request.PeriodStartUtc.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["identifier"] = request.ExternalIdempotencyKey
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "billing/meter_events")
        {
            Content = new FormUrlEncodedContent(payload)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        message.Headers.TryAddWithoutValidation("Idempotency-Key", request.ExternalIdempotencyKey);

        try
        {
            using var response = await httpClientFactory
                .CreateClient(nameof(StripeCustomerBillingTransport))
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var requestId = ReadStripeRequestId(response);
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
                    var root = document.RootElement;
                    var externalObjectId = root.TryGetProperty("identifier", out var identifierElement)
                        ? identifierElement.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(externalObjectId))
                    {
                        return Result(
                            CustomerBillingExternalTransportOutcome.Indeterminate,
                            "stripe_success_response_missing_identifier",
                            externalRequestId: requestId);
                    }

                    return Result(
                        CustomerBillingExternalTransportOutcome.Delivered,
                        "stripe_meter_event_accepted",
                        externalObjectId,
                        requestId);
                }
                catch (JsonException)
                {
                    return Result(
                        CustomerBillingExternalTransportOutcome.Indeterminate,
                        "stripe_success_response_invalid",
                        externalRequestId: requestId);
                }
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return Result(
                    CustomerBillingExternalTransportOutcome.RetryableUnavailable,
                    "stripe_rate_limited",
                    externalRequestId: requestId);
            }

            if ((int)response.StatusCode >= 500)
            {
                return Result(
                    CustomerBillingExternalTransportOutcome.Indeterminate,
                    "stripe_server_outcome_ambiguous",
                    externalRequestId: requestId);
            }

            if ((int)response.StatusCode is >= 400 and < 500)
            {
                return Result(
                    CustomerBillingExternalTransportOutcome.Rejected,
                    "stripe_request_rejected",
                    externalRequestId: requestId);
            }

            return Result(
                CustomerBillingExternalTransportOutcome.Indeterminate,
                "stripe_unexpected_http_status",
                externalRequestId: requestId);
        }
        catch (HttpRequestException)
        {
            return Result(CustomerBillingExternalTransportOutcome.Indeterminate, "stripe_network_outcome_ambiguous");
        }
        catch (OperationCanceledException)
        {
            return Result(CustomerBillingExternalTransportOutcome.Indeterminate, "stripe_request_outcome_ambiguous");
        }
    }

    public Task<CustomerBillingExternalReconciliationResult> ReconcileAsync(
        CustomerBillingExternalReconciliationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ExternalIdempotencyKey)
            || request.DeliveryId == Guid.Empty)
        {
            throw new ArgumentException("A delivery ID and persisted idempotency key are required.", nameof(request));
        }

        return Task.FromResult(new CustomerBillingExternalReconciliationResult(
            CustomerBillingExternalReconciliationOutcome.Unsupported,
            "stripe_meter_event_lookup_not_supported"));
    }

    private static (CustomerBillingExternalMapping? Mapping, CustomerBillingExternalResult? Error) ResolveMapping(
        CustomerBillingExternalRequest request,
        CustomerUsageBillingOptions options)
    {
        var metricName = request.Metric.ToString();
        if (!Enum.IsDefined(request.Metric))
        {
            return (null, Result(
                CustomerBillingExternalTransportOutcome.ConfigurationError,
                "usage_metric_mapping_missing"));
        }

        var configured = options.Metrics
            .FirstOrDefault(entry => string.Equals(entry.Key, metricName, StringComparison.Ordinal))
            .Value;
        if (configured is null)
        {
            return (null, Result(
                CustomerBillingExternalTransportOutcome.ConfigurationError,
                "usage_metric_mapping_missing"));
        }

        if (request.Mapping is null
            || !CustomerUsageBillingOptionsValidator.IsValidMapping(configured)
            || !string.Equals(request.Mapping.Transport, configured.Transport, StringComparison.Ordinal)
            || !string.Equals(request.Mapping.MeterEventName, configured.MeterEventName, StringComparison.Ordinal)
            || !string.Equals(request.Mapping.CustomerPayloadKey, configured.CustomerPayloadKey, StringComparison.Ordinal)
            || !string.Equals(request.Mapping.ValuePayloadKey, configured.ValuePayloadKey, StringComparison.Ordinal)
            || request.Mapping.BillableQuantityIsExternalUnit != configured.BillableQuantityIsExternalUnit)
        {
            return (null, Result(
                CustomerBillingExternalTransportOutcome.ConfigurationError,
                "usage_metric_mapping_invalid"));
        }

        return (new CustomerBillingExternalMapping(
            configured.Transport,
            configured.MeterEventName,
            configured.CustomerPayloadKey,
            configured.ValuePayloadKey,
            configured.BillableQuantityIsExternalUnit), null);
    }

    private static string? ReadStripeRequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Request-Id", out var values)
            ? values.FirstOrDefault()
            : null;

    private static CustomerBillingExternalResult Result(
        CustomerBillingExternalTransportOutcome outcome,
        string reasonCode,
        string? externalObjectId = null,
        string? externalRequestId = null) =>
        new(outcome, reasonCode, externalObjectId, externalRequestId);
}
