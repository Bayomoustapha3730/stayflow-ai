using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Tests;

public sealed class StripeCustomerBillingTransportTests
{
    private static readonly Guid TestPlanId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid TestPriceVersionId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    [Theory]
    [InlineData("mtr_test_61Vf7xPobjbV78dGn418JaYiexAe686y")]
    [InlineData("mtr_abc123")]
    [InlineData("mtr_test123")]
    public void SupportedMeterIdentifiers_PassOptionsValidation(string meterId)
    {
        var result = new CustomerUsageBillingOptionsValidator().Validate(null, OptionsWithMeterId(meterId));

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Failures ?? []));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("mtr")]
    [InlineData("mtr_")]
    [InlineData("mtr-test_abc")]
    [InlineData("meter_test_abc")]
    [InlineData("prod_test_abc")]
    [InlineData("price_test_abc")]
    [InlineData("cus_test_abc")]
    [InlineData("sub_test_abc")]
    [InlineData("https://example.com/mtr_test_abc")]
    [InlineData("mtr_test_abc/def")]
    [InlineData("mtr_test_abc def")]
    [InlineData("mtr_test_")]
    [InlineData("mtr_test__abc")]
    [InlineData("mtr_test_abc_def")]
    [InlineData("mtr_test_abc\n")]
    [InlineData("mtr_abc\n")]
    [InlineData("mtr_test_abc\r")]
    [InlineData("mtr_test_abc\t")]
    [InlineData("mtr_test_abc\0")]
    public void MalformedMeterIdentifiers_FailOptionsValidation(string? meterId)
    {
        var result = new CustomerUsageBillingOptionsValidator().Validate(null, OptionsWithMeterId(meterId));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.EndsWith(":MeterId must be a Stripe meter ID.", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidTestMeterIdentifier_DoesNotAttestUnverifiedResources()
    {
        var options = OptionsWithMeterId("mtr_test_61Vf7xPobjbV78dGn418JaYiexAe686y");
        options.Metrics["Reservations"].PriceMappings.Values.Single().ResourcesVerified = false;

        var result = new CustomerUsageBillingOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Single(result.Failures!);
        Assert.Contains(":ResourcesVerified requires operational verification", result.Failures!.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void VersionAwareMapping_RoundTripsThroughRealJsonConfigurationBinding()
    {
        var source = EnabledOptions();
        using var json = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(source)));
        var configuration = new ConfigurationBuilder().AddJsonStream(json).Build();
        var bound = configuration.Get<CustomerUsageBillingOptions>()!;

        Assert.True(new CustomerUsageBillingOptionsValidator().Validate(null, bound).Succeeded);
        var key = CustomerUsageBillingOptionsValidator.PriceMappingKey(TestPlanId, TestPriceVersionId);
        Assert.Equal("price_test123", bound.Metrics["AiRequests"].PriceMappings[key].UsagePriceId);
        Assert.Equal(source.Metrics["AiRequests"].PriceMappings[key].MappingIdentity,
            bound.Metrics["AiRequests"].PriceMappings[key].MappingIdentity);
    }

    [Theory]
    [InlineData("AiTokens")]
    [InlineData("ApiRequests")]
    [InlineData("StorageBytes")]
    [InlineData("FileUploads")]
    [InlineData("Users")]
    [InlineData("Properties")]
    public void UnprovenAndCapacityMetrics_CannotBeExternallyMapped(string metric)
    {
        var options = new CustomerUsageBillingOptions { Metrics = { [metric] = Mapping() } };
        Assert.False(new CustomerUsageBillingOptionsValidator().Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(false, true, "invoice_finalization_policy_unverified")]
    [InlineData(true, false, "usage_price_mapping_invalid")]
    public async Task UnverifiedOperationalPolicies_FailClosed(
        bool invoiceSafetyVerified, bool resourcesVerified, string expectedReason)
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var options = EnabledOptions();
        options.OpenPeriodInvoiceSafetyVerified = invoiceSafetyVerified;
        options.Metrics["AiRequests"].PriceMappings.Values.Single().ResourcesVerified = resourcesVerified;

        var result = await CreateTransport(harness, http, options).SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal(expectedReason, result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Theory]
    [InlineData(28, "usage_period_closed")]
    [InlineData(29, "usage_period_closed")]
    [InlineData(40, "usage_occurrence_outside_stripe_timestamp_window")]
    [InlineData(0, "usage_occurrence_outside_stripe_timestamp_window")]
    public async Task ClosedPeriodAndDelayedOrFutureOccurrence_FailClosed(int dispatchDay, string expectedReason)
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();

        var result = await CreateTransport(harness, http, EnabledOptions(), dispatchTime: At(dispatchDay))
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal(expectedReason, result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task UnpinnedApiVersion_FailsClosedWithoutHttp()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var transport = new StripeCustomerBillingTransport(http,
            Options.Create(new BillingOptions { Provider = "Stripe", StripeApiVersion = "" }),
            Options.Create(EnabledOptions()), harness.Context, new FixedTimeProvider(At(10)));

        var result = await transport.SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal("stripe_api_version_invalid", result.ReasonCode);
        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public void RoundedStripeRate_CannotPassExactParity()
    {
        var options = EnabledOptions();
        var mapping = options.Metrics["AiRequests"].PriceMappings.Values.Single();
        mapping.UnitQuantity = 3m;
        mapping.UnitPrice = 1m;
        mapping.StripeUnitAmountMinor = 33.333333333333m;

        Assert.False(new CustomerUsageBillingOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public async Task MissingPriceMapping_DoesNotFallBackToCurrentPrice()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var options = EnabledOptions();
        var mapping = options.Metrics["AiRequests"].PriceMappings.Values.Single();
        options.Metrics["AiRequests"].PriceMappings.Clear();
        var currentVersionId = Guid.NewGuid();
        mapping.MappingIdentity = CustomerUsageBillingOptionsValidator.ExpectedMappingIdentity(
            TestPlanId, UsageMetric.AiRequests, currentVersionId, mapping.MeterId, mapping.MeterEventName, mapping.UsagePriceId);
        options.Metrics["AiRequests"].PriceMappings.Add(
            CustomerUsageBillingOptionsValidator.PriceMappingKey(TestPlanId, currentVersionId), mapping);

        var result = await CreateTransport(harness, http, options).SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal("usage_price_mapping_missing", result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task MidPeriodPriceChange_DoesNotReRateHistoricalOperation()
    {
        await using var harness = await CreateHarnessAsync();
        var laterPrice = CustomerUsagePriceVersion.Create(new StayFlow.Api.Services.CustomerUsagePriceVersionDraft(
            TestPlanId, UsageMetric.AiRequests, CustomerUsageBillingMode.PerUnitOverage,
            1m, 99m, "USD", At(5), null));
        harness.Context.CustomerUsagePriceVersions.Add(laterPrice);
        await harness.Context.SaveChangesAsync();
        using var http = new RecordingHttpClientFactory();

        var result = await CreateTransport(harness, http, EnabledOptions())
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.Delivered, result.Outcome);
        Assert.Equal(1, http.RequestCount);
        Assert.Contains("payload%5Bvalue%5D=7", http.Body);
    }

    [Theory]
    [InlineData("basis")]
    [InlineData("identity")]
    [InlineData("semantics")]
    [InlineData("rate")]
    public async Task PriceParity_MetadataDriftCannotSend(string mismatch)
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var options = EnabledOptions();
        var mapping = options.Metrics["AiRequests"].PriceMappings.Values.Single();
        var requestMapping = harness.Request.Mapping;
        switch (mismatch)
        {
            case "basis":
                mapping.UnitQuantity = 2m;
                mapping.StripeUnitAmountMinor = 62.5m;
                requestMapping = requestMapping with { UnitQuantity = 2m, StripeUnitAmountMinor = 62.5m };
                break;
            case "identity":
                mapping.MappingIdentity = "unverified";
                break;
            case "semantics":
                mapping.RateSemantics = "Tiered";
                break;
            case "rate":
                mapping.StripeUnitAmountMinor = 126m;
                break;
        }

        var result = await CreateTransport(harness, http, options)
            .SendAsync(harness.Request with { Mapping = requestMapping }, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task StripeMissingResourceBadRequest_IsConfigurationError()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":{"type":"invalid_request_error","code":"resource_missing"}}""")
        });

        var result = await CreateTransport(harness, http, EnabledOptions())
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal(1, http.RequestCount);
    }

    [Fact]
    public async Task DisabledUsageBilling_ReturnsDisabledWithoutHttp()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var result = await CreateTransport(harness, http, new CustomerUsageBillingOptions())
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.Disabled, result.Outcome);
        Assert.Equal("usage_billing_disabled", result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task MissingExactMetricMapping_FailsClosedWithoutHttp()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var options = new CustomerUsageBillingOptions { Enabled = true };
        var result = await CreateTransport(harness, http, options)
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal("usage_metric_mapping_missing", result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task CaseInsensitiveDictionaryDoesNotEnableFuzzyMetricMapping()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var options = new CustomerUsageBillingOptions
        {
            Enabled = true,
            Metrics = new Dictionary<string, CustomerUsageBillingMetricOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["airequests"] = Mapping()
            }
        };

        var result = await CreateTransport(harness, http, options)
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal("usage_price_mapping_invalid", result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public void InvalidMetricCaseOrImplicitUnitMapping_IsRejectedByOptionsValidation()
    {
        var validator = new CustomerUsageBillingOptionsValidator();
        var options = Options.Create(new CustomerUsageBillingOptions
        {
            Enabled = true,
            Metrics = new Dictionary<string, CustomerUsageBillingMetricOptions>(StringComparer.Ordinal)
            {
                ["aitokens"] = Mapping(),
                ["AiRequests"] = new CustomerUsageBillingMetricOptions
                {
                    Transport = "StripeMeter",
                    BillableQuantityIsExternalUnit = false
                }
            }
        });

        var result = validator.Validate(null, options.Value);

        Assert.False(result.Succeeded);
        var failures = string.Join(Environment.NewLine, result.Failures ?? []);
        Assert.Contains("aitokens", failures, StringComparison.Ordinal);
        Assert.Contains("BillableQuantityIsExternalUnit", failures, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TenantMismatchAndIneligibleSubscription_FailClosedWithoutHttp()
    {
        await using (var mismatched = await CreateHarnessAsync())
        {
            using var http = new RecordingHttpClientFactory();
            var result = await CreateTransport(mismatched, http, EnabledOptions())
                .SendAsync(mismatched.Request with { CompanyId = Guid.NewGuid() }, CancellationToken.None);
            Assert.Equal("delivery_snapshot_mismatch", result.ReasonCode);
            Assert.Equal(0, http.RequestCount);
        }

        await using (var ineligible = await CreateHarnessAsync(subscriptionStatus: SubscriptionStatus.Cancelled))
        {
            using var http = new RecordingHttpClientFactory();
            var result = await CreateTransport(ineligible, http, EnabledOptions())
                .SendAsync(ineligible.Request, CancellationToken.None);
            Assert.Equal("subscription_not_eligible", result.ReasonCode);
            Assert.Equal(0, http.RequestCount);
        }
    }

    [Fact]
    public async Task HistoricalPlanSnapshotStillRoutesAfterSubscriptionPlanChanges()
    {
        await using var harness = await CreateHarnessAsync();
        var currentSubscription = await harness.Context.TenantSubscriptions.SingleAsync();
        var changedPlan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Changed plan",
            DisplayName = "Changed plan",
            Description = "Current mutable plan"
        };
        harness.Context.SubscriptionPlans.Add(changedPlan);
        currentSubscription.SubscriptionPlanId = changedPlan.Id;
        await harness.Context.SaveChangesAsync();
        using var http = new RecordingHttpClientFactory();

        var result = await CreateTransport(harness, http, EnabledOptions())
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.Delivered, result.Outcome);
        Assert.Equal(1, http.RequestCount);
    }

    [Fact]
    public async Task MissingHistoricalPriceVersionMapping_FailsClosed()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();

        var result = await CreateTransport(harness, http, EnabledOptions())
            .SendAsync(harness.Request with { CustomerUsagePriceVersionId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal("usage_price_mapping_missing", result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task NonUsdUsagePriceMapping_FailsClosed()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var options = EnabledOptions();
        options.Metrics["AiRequests"].PriceMappings.Values.Single().Currency = "EUR";

        var result = await CreateTransport(harness, http, options)
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal("usage_price_mapping_invalid", result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task ConfiguredPriceRateThatDiffersFromPersistedPricingSnapshot_FailsClosed()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var options = EnabledOptions();
        var priceMapping = options.Metrics["AiRequests"].PriceMappings.Values.Single();
        priceMapping.UnitPrice = 2m;
        priceMapping.StripeUnitAmountMinor = 200m;
        priceMapping.MappingIdentity = CustomerUsageBillingOptionsValidator.ExpectedMappingIdentity(
            TestPlanId, UsageMetric.AiRequests, TestPriceVersionId,
            "mtr_test123", "ai_requests", "price_test123");
        var requestMapping = harness.Request.Mapping with
        {
            UnitPrice = 2m,
            StripeUnitAmountMinor = 200m,
            MappingIdentity = priceMapping.MappingIdentity
        };

        var result = await CreateTransport(harness, http, options)
            .SendAsync(harness.Request with { Mapping = requestMapping }, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal("usage_price_parity_mismatch", result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task DeliveryTimestampMustBeOperationOccurrenceNotPeriodStart()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();

        var result = await CreateTransport(harness, http, EnabledOptions())
            .SendAsync(harness.Request with { OccurredAtUtc = At(0) }, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal("delivery_snapshot_mismatch", result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task MissingStripeSubscriptionOrCustomerMappings_FailClosedWithoutHttp()
    {
        await using (var noSubscriptionId = await CreateHarnessAsync(externalSubscriptionId: null))
        {
            using var http = new RecordingHttpClientFactory();
            var result = await CreateTransport(noSubscriptionId, http, EnabledOptions())
                .SendAsync(noSubscriptionId.Request, CancellationToken.None);
            Assert.Equal("stripe_subscription_mapping_missing", result.ReasonCode);
            Assert.Equal(0, http.RequestCount);
        }

        await using (var noCustomerId = await CreateHarnessAsync(stripeCustomerId: null))
        {
            using var http = new RecordingHttpClientFactory();
            var result = await CreateTransport(noCustomerId, http, EnabledOptions())
                .SendAsync(noCustomerId.Request, CancellationToken.None);
            Assert.Equal("stripe_customer_mapping_missing", result.ReasonCode);
            Assert.Equal(0, http.RequestCount);
        }
    }

    [Fact]
    public async Task AcceptedMeterEvent_UsesSnapshotQuantityAndPersistedIdempotencyKeyOnly()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"object\":\"billing.meter_event\",\"identifier\":\"" +
                harness.Request.ExternalIdempotencyKey + "\"}")
        });

        var result = await CreateTransport(harness, http, EnabledOptions())
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.Delivered, result.Outcome);
        Assert.Equal(harness.Request.ExternalIdempotencyKey, result.ExternalObjectId);
        Assert.Equal("req_123", result.ExternalRequestId);
        Assert.Equal(1, http.RequestCount);
        Assert.Equal(HttpMethod.Post, http.Method);
        Assert.Equal("/v1/billing/meter_events", http.PathAndQuery);
        Assert.Equal(harness.Request.ExternalIdempotencyKey, http.IdempotencyKey);
        Assert.Equal("2024-06-20", http.StripeVersion);
        Assert.Contains($"identifier={Uri.EscapeDataString(harness.Request.ExternalIdempotencyKey)}", http.Body);
        Assert.Contains("payload%5Bvalue%5D=7", http.Body);
        Assert.Contains("payload%5Bstripe_customer_id%5D=cus_test_123", http.Body);
        Assert.Contains("event_name=ai_requests", http.Body);
        Assert.Contains($"timestamp={At(1).ToUnixTimeSeconds()}", http.Body);
        Assert.DoesNotContain("payload%5Bvalue%5D=10", http.Body);
        Assert.DoesNotContain("price", http.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("charge", http.Body, StringComparison.OrdinalIgnoreCase);
        var delivery = await harness.Context.CustomerBillingDeliveries.SingleAsync();
        var operation = await harness.Context.CustomerBillableUsageOperations.SingleAsync();
        Assert.Equal(operation.OccurredAtUtc, delivery.OccurredAtUtc);
        Assert.Equal(operation.OccurredAtUtc, harness.Request.OccurredAtUtc);
        Assert.NotEqual(delivery.PeriodStartUtc, harness.Request.OccurredAtUtc);
        Assert.NotEqual(delivery.CreatedAtUtc, harness.Request.OccurredAtUtc);
        Assert.DoesNotContain(typeof(CustomerBillingExternalRequest).GetProperties(),
            property => property.Name.Contains("ProviderCost", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("idempotency_error", "idempotency_key_in_use")]
    [InlineData("invalid_request_error", "billing_meter_event_duplicate")]
    [InlineData("invalid_request_error", "unknown_code")]
    public async Task DuplicateAndUnprovenBadRequests_AreIndeterminate(string type, string code)
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { error = new { type, code } }))
        });

        var result = await CreateTransport(harness, http, EnabledOptions())
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.Indeterminate, result.Outcome);
        Assert.Equal(1, http.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, CustomerBillingExternalTransportOutcome.Rejected, "stripe_request_rejected")]
    [InlineData(HttpStatusCode.Unauthorized, CustomerBillingExternalTransportOutcome.ConfigurationError, "stripe_credentials_rejected")]
    [InlineData(HttpStatusCode.Forbidden, CustomerBillingExternalTransportOutcome.ConfigurationError, "stripe_permissions_rejected")]
    [InlineData(HttpStatusCode.NotFound, CustomerBillingExternalTransportOutcome.ConfigurationError, "stripe_usage_configuration_not_found")]
    [InlineData(HttpStatusCode.TooManyRequests, CustomerBillingExternalTransportOutcome.RetryableUnavailable, "stripe_rate_limited")]
    [InlineData(HttpStatusCode.Conflict, CustomerBillingExternalTransportOutcome.Indeterminate, "stripe_conflict_outcome_ambiguous")]
    [InlineData(HttpStatusCode.RequestTimeout, CustomerBillingExternalTransportOutcome.Indeterminate, "stripe_unexpected_http_status")]
    [InlineData(HttpStatusCode.InternalServerError, CustomerBillingExternalTransportOutcome.Indeterminate, "stripe_server_outcome_ambiguous")]
    public async Task HttpFailureStatuses_AreClassifiedWithoutRetry(
        HttpStatusCode status,
        CustomerBillingExternalTransportOutcome expected,
        string reasonCode)
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory((_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent("""{"error":{"type":"invalid_request_error","code":"parameter_missing"}}""")
        });

        var result = await CreateTransport(harness, http, EnabledOptions())
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(reasonCode, result.ReasonCode);
        Assert.Equal(1, http.RequestCount);
    }

    [Fact]
    public async Task AmbiguousNetworkFailure_IsIndeterminateAndDoesNotTriggerSecondPost()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory((_, _) => throw new HttpRequestException("ambiguous"));

        var result = await CreateTransport(harness, http, EnabledOptions())
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.Indeterminate, result.Outcome);
        Assert.Equal("stripe_network_outcome_ambiguous", result.ReasonCode);
        Assert.Equal(1, http.RequestCount);
    }

    [Fact]
    public async Task AmbiguousTimeout_IsIndeterminateAndDoesNotTriggerSecondPost()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory((_, _) =>
            throw new TaskCanceledException("timeout before response"));

        var result = await CreateTransport(harness, http, EnabledOptions())
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.Indeterminate, result.Outcome);
        Assert.Equal("stripe_request_outcome_ambiguous", result.ReasonCode);
        Assert.Equal(1, http.RequestCount);
    }

    [Fact]
    public async Task Reconcile_IsExplicitlyUnsupportedAndNeverIssuesHttp()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var result = await CreateTransport(harness, http, EnabledOptions())
            .ReconcileAsync(new CustomerBillingExternalReconciliationRequest(
                harness.Request.ExternalIdempotencyKey,
                null,
                harness.Request.DeliveryId), CancellationToken.None);

        Assert.Equal(CustomerBillingExternalReconciliationOutcome.Unsupported, result.Outcome);
        Assert.Equal("stripe_meter_event_lookup_not_supported", result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task MissingStripeSecret_FailsClosedWithoutHttp()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var result = await CreateTransport(harness, http, EnabledOptions(), stripeSecretKey: string.Empty)
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal("stripe_secret_missing", result.ReasonCode);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task DevelopmentTransport_IsDeterministicAndNeverUsesHttp()
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory();
        var transport = new DevelopmentCustomerBillingTransport();

        var first = await transport.SendAsync(harness.Request, CancellationToken.None);
        var replay = await transport.SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.Simulated, first.Outcome);
        Assert.Equal("development_transport_no_external_call", first.ReasonCode);
        Assert.Equal(first, replay);
        Assert.Equal(0, http.RequestCount);
    }

    private static StripeCustomerBillingTransport CreateTransport(
        Harness harness,
        IHttpClientFactory httpClientFactory,
        CustomerUsageBillingOptions usageOptions,
        string stripeSecretKey = "sk_test_never_logged",
        DateTimeOffset? dispatchTime = null) =>
        new(
            httpClientFactory,
            Options.Create(new BillingOptions
            {
                Provider = "Stripe",
                StripeSecretKey = stripeSecretKey
            }),
            Options.Create(usageOptions),
            harness.Context,
            new FixedTimeProvider(dispatchTime ?? At(10)));

    private static CustomerUsageBillingOptions OptionsWithMeterId(string? meterId)
    {
        var metric = Mapping();
        var price = metric.PriceMappings.Values.Single();
        price.MeterId = meterId!;
        price.MappingIdentity = CustomerUsageBillingOptionsValidator.ExpectedMappingIdentity(
            TestPlanId, UsageMetric.Reservations, TestPriceVersionId,
            price.MeterId, price.MeterEventName, price.UsagePriceId);
        return new CustomerUsageBillingOptions { Metrics = { ["Reservations"] = metric } };
    }

    private static CustomerUsageBillingOptions EnabledOptions() => new()
    {
        Enabled = true,
        OpenPeriodInvoiceSafetyVerified = true,
        Metrics = new Dictionary<string, CustomerUsageBillingMetricOptions>(StringComparer.Ordinal)
        {
            ["AiRequests"] = Mapping()
        }
    };

    private static CustomerUsageBillingMetricOptions Mapping() => new()
    {
        Transport = "StripeMeter",
        CustomerPayloadKey = "stripe_customer_id",
        ValuePayloadKey = "value",
        BillableQuantityIsExternalUnit = true,
        PriceMappings = new Dictionary<string, CustomerUsageBillingPriceMappingOptions>(StringComparer.Ordinal)
        {
            [CustomerUsageBillingOptionsValidator.PriceMappingKey(TestPlanId, TestPriceVersionId)] = new()
            {
                ResourcesVerified = true,
                MeterId = "mtr_test123",
                MeterEventName = "ai_requests",
                UsagePriceId = "price_test123",
                Currency = "USD",
                UnitQuantity = 1m,
                UnitPrice = 1.25m,
                StripeUnitAmountMinor = 125m,
                RateSemantics = "LinearPerBillableUnit",
                MappingIdentity = CustomerUsageBillingOptionsValidator.ExpectedMappingIdentity(
                    TestPlanId, UsageMetric.AiRequests, TestPriceVersionId,
                    "mtr_test123", "ai_requests", "price_test123")
            }
        }
    };

    private static async Task<Harness> CreateHarnessAsync(
        string? externalSubscriptionId = "sub_test_123",
        string? stripeCustomerId = "cus_test_123",
        SubscriptionStatus subscriptionStatus = SubscriptionStatus.Active)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"stripe-customer-billing-{Guid.NewGuid():N}")
            .Options;
        var companyId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var planId = TestPlanId;
        var priceVersionId = TestPriceVersionId;
        var company = new Company
        {
            Id = companyId,
            Name = "Transport test",
            Slug = $"transport-{companyId:N}",
            NormalizedSlug = $"TRANSPORT-{companyId:N}",
            Email = $"{companyId:N}@transport.test",
            PhoneNumber = "0000000000",
            CountryCode = "US",
            TimeZone = "UTC",
            StripeCustomerId = stripeCustomerId
        };
        var subscription = new TenantSubscription
        {
            Id = subscriptionId,
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            Status = subscriptionStatus.ToStorageValue(),
            CurrentPeriodStartUtc = At(0),
            CurrentPeriodEndUtc = At(28),
            ExternalSubscriptionId = externalSubscriptionId
        };
        var plan = new SubscriptionPlan
        {
            Id = planId,
            Name = $"Transport-{planId:N}",
            DisplayName = "Transport test",
            Description = "Transport test plan"
        };
        var priceVersion = CustomerUsagePriceVersion.Create(new StayFlow.Api.Services.CustomerUsagePriceVersionDraft(
            planId,
            UsageMetric.AiRequests,
            CustomerUsageBillingMode.PerUnitOverage,
            1m,
            1.25m,
            "USD",
            At(0),
            null));
        priceVersion.Id = priceVersionId;

        var usageOperation = new UsageOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Metric = UsageMetric.AiRequests.ToStorageValue(),
            IdempotencyKey = $"transport-{Guid.NewGuid():N}",
            PeriodStartUtc = At(0),
            Quantity = 10
        };
        var billableOperation = CustomerBillableUsageOperation.Create(
            companyId,
            usageOperation,
            subscriptionId,
            planId,
            UsageMetric.AiRequests,
            At(1).AddTicks(1234560),
            At(28),
            10,
            0,
            10,
            3,
            7,
            CustomerBillableUsageState.Calculated,
            CustomerBillableUsageReason.Calculated,
            priceVersionId,
            1m,
            1.25m,
            8.75m,
            "USD");
        var delivery = CustomerBillingDelivery.Create(
            billableOperation,
            CustomerBillingDeliveryState.Dispatching,
            CustomerBillingDeliveryReason.DispatchClaimed,
            At(10));
        var mapping = Mapping();
        var context = new ApplicationDbContext(options);
        context.Companies.Add(company);
        context.SubscriptionPlans.Add(plan);
        context.CustomerUsagePriceVersions.Add(priceVersion);
        context.TenantSubscriptions.Add(subscription);
        context.UsageOperations.Add(usageOperation);
        context.CustomerBillableUsageOperations.Add(billableOperation);
        context.CustomerBillingDeliveries.Add(delivery);
        await context.SaveChangesAsync();

        return new Harness(
            context,
            CustomerBillingExternalRequest.FromDelivery(delivery, new CustomerBillingExternalMapping(
                mapping.Transport,
                    mapping.PriceMappings[CustomerUsageBillingOptionsValidator.PriceMappingKey(TestPlanId, TestPriceVersionId)].MeterEventName,
                mapping.CustomerPayloadKey,
                mapping.ValuePayloadKey,
                    mapping.BillableQuantityIsExternalUnit)
            {
                PlanSnapshotId = TestPlanId,
                CustomerUsagePriceVersionId = TestPriceVersionId,
                MeterId = "mtr_test123",
                UsagePriceId = "price_test123",
                Currency = "USD",
                UnitQuantity = 1m,
                UnitPrice = 1.25m,
                StripeUnitAmountMinor = 125m,
                RateSemantics = "LinearPerBillableUnit",
                MappingIdentity = CustomerUsageBillingOptionsValidator.ExpectedMappingIdentity(
                        TestPlanId, UsageMetric.AiRequests, TestPriceVersionId,
                        "mtr_test123", "ai_requests", "price_test123")
            }));
    }

    private static DateTimeOffset At(int days) =>
        new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days);

    private sealed record Harness(
        ApplicationDbContext Context,
        CustomerBillingExternalRequest Request) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient _client;
        private readonly RecordingHandler _handler;

        public RecordingHttpClientFactory(
            Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>? responseFactory = null)
        {
            _handler = new RecordingHandler(responseFactory);
            _client = new HttpClient(_handler)
            {
                BaseAddress = new Uri("https://api.stripe.com/v1/")
            };
        }

        public int RequestCount => _handler.RequestCount;
        public HttpMethod? Method => _handler.Method;
        public string? PathAndQuery => _handler.PathAndQuery;
        public string? IdempotencyKey => _handler.IdempotencyKey;
        public string? StripeVersion => _handler.StripeVersion;
        public string Body => _handler.Body;

        public HttpClient CreateClient(string name) => _client;

        public void Dispose() => _client.Dispose();

        private sealed class RecordingHandler(
            Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>? responseFactory) : HttpMessageHandler
        {
            public int RequestCount { get; private set; }
            public HttpMethod? Method { get; private set; }
            public string? PathAndQuery { get; private set; }
            public string? IdempotencyKey { get; private set; }
            public string? StripeVersion { get; private set; }
            public string Body { get; private set; } = string.Empty;

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                RequestCount++;
                Method = request.Method;
                PathAndQuery = request.RequestUri?.PathAndQuery;
                IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var values)
                    ? values.Single()
                    : null;
                StripeVersion = request.Headers.TryGetValues("Stripe-Version", out var versionValues)
                    ? versionValues.Single()
                    : null;
                Body = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                var response = responseFactory?.Invoke(request, cancellationToken)
                    ?? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"identifier\":\"stripe-event-1\"}")
                    };
                response.Headers.TryAddWithoutValidation("Request-Id", "req_123");
                return response;
            }
        }
    }
}
