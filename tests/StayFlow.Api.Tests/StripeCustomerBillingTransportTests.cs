using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Tests;

public sealed class StripeCustomerBillingTransportTests
{
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
                ["aitokens"] = Mapping()
            }
        };

        var result = await CreateTransport(harness, http, options)
            .SendAsync(harness.Request, CancellationToken.None);

        Assert.Equal(CustomerBillingExternalTransportOutcome.ConfigurationError, result.Outcome);
        Assert.Equal("usage_metric_mapping_missing", result.ReasonCode);
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
                    MeterEventName = "ai_requests",
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
        Assert.Contains($"identifier={Uri.EscapeDataString(harness.Request.ExternalIdempotencyKey)}", http.Body);
        Assert.Contains("payload%5Bvalue%5D=7", http.Body);
        Assert.Contains("payload%5Bstripe_customer_id%5D=cus_test_123", http.Body);
        Assert.Contains("event_name=ai_tokens", http.Body);
        Assert.Contains($"timestamp={At(0).ToUnixTimeSeconds()}", http.Body);
        Assert.DoesNotContain("price", http.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("charge", http.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, CustomerBillingExternalTransportOutcome.Rejected, "stripe_request_rejected")]
    [InlineData(HttpStatusCode.TooManyRequests, CustomerBillingExternalTransportOutcome.RetryableUnavailable, "stripe_rate_limited")]
    [InlineData(HttpStatusCode.InternalServerError, CustomerBillingExternalTransportOutcome.Indeterminate, "stripe_server_outcome_ambiguous")]
    public async Task HttpFailureStatuses_AreClassifiedWithoutRetry(
        HttpStatusCode status,
        CustomerBillingExternalTransportOutcome expected,
        string reasonCode)
    {
        await using var harness = await CreateHarnessAsync();
        using var http = new RecordingHttpClientFactory((_, _) => new HttpResponseMessage(status));

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
        string stripeSecretKey = "sk_test_never_logged") =>
        new(
            httpClientFactory,
            Options.Create(new BillingOptions
            {
                Provider = "Stripe",
                StripeSecretKey = stripeSecretKey
            }),
            Options.Create(usageOptions),
            harness.Context,
            new FixedTimeProvider(At(10)));

    private static CustomerUsageBillingOptions EnabledOptions() => new()
    {
        Enabled = true,
        Metrics = new Dictionary<string, CustomerUsageBillingMetricOptions>(StringComparer.Ordinal)
        {
            ["AiTokens"] = Mapping()
        }
    };

    private static CustomerUsageBillingMetricOptions Mapping() => new()
    {
        Transport = "StripeMeter",
        MeterEventName = "ai_tokens",
        CustomerPayloadKey = "stripe_customer_id",
        ValuePayloadKey = "value",
        BillableQuantityIsExternalUnit = true
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
        var planId = Guid.NewGuid();
        var priceVersionId = Guid.NewGuid();
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
            UsageMetric.AiTokens,
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
            Metric = UsageMetric.AiTokens.ToStorageValue(),
            IdempotencyKey = $"transport-{Guid.NewGuid():N}",
            PeriodStartUtc = At(0),
            Quantity = 10
        };
        var billableOperation = CustomerBillableUsageOperation.Create(
            companyId,
            usageOperation,
            subscriptionId,
            planId,
            UsageMetric.AiTokens,
            At(1),
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
                mapping.MeterEventName,
                mapping.CustomerPayloadKey,
                mapping.ValuePayloadKey,
                mapping.BillableQuantityIsExternalUnit)));
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
