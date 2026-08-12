using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Billing;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Tests;

public sealed class BillingServiceTests
{
    [Fact]
    public async Task ProcessStripeWebhookAsync_SecondDelivery_IsIdempotent()
    {
        var fixture = await CreateFixtureAsync(
            configuredOptions: new BillingOptions
            {
                Provider = "Development",
                StripeWebhookSigningSecret = "whsec_test",
                CheckoutSuccessUrl = "https://example.test/success",
                CheckoutCancelUrl = "https://example.test/cancel",
                BillingPortalReturnUrl = "https://example.test/portal",
                PlanPriceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Starter"] = "price_starter",
                    ["Growth"] = "price_growth",
                    ["Scale"] = "price_scale"
                }
            },
            provider: null);

        var first = await fixture.Service.ProcessStripeWebhookAsync(fixture.RawWebhookPayload, fixture.ValidSignatureHeader, CancellationToken.None);
        var second = await fixture.Service.ProcessStripeWebhookAsync(fixture.RawWebhookPayload, fixture.ValidSignatureHeader, CancellationToken.None);

        Assert.False(first.WasDuplicate);
        Assert.True(second.WasDuplicate);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ReturnsActionableError_WhenPriceMappingMissing()
    {
        var fixture = await CreateFixtureAsync(
            configuredOptions: new BillingOptions
            {
                Provider = "Stripe",
                StripeSecretKey = "sk_test_123",
                StripeWebhookSigningSecret = "whsec_test",
                CheckoutSuccessUrl = "https://example.test/success",
                CheckoutCancelUrl = "https://example.test/cancel",
                BillingPortalReturnUrl = "https://example.test/portal",
                PlanPriceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Starter"] = "price_starter",
                    ["Growth"] = "price_growth"
                }
            });

        var response = await fixture.Service.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest
        {
            PlanName = "Scale"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("Scale", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Billing:PlanPriceIds", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ReturnsCapabilityMessage_WhenStripeConfigurationMissing()
    {
        var fixture = await CreateFixtureAsync(
            configuredOptions: new BillingOptions
            {
                Provider = "Development",
                StripeSecretKey = string.Empty,
                StripeWebhookSigningSecret = string.Empty,
                CheckoutSuccessUrl = "https://example.test/success",
                CheckoutCancelUrl = "https://example.test/cancel",
                BillingPortalReturnUrl = "https://example.test/portal",
                PlanPriceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            });

        var response = await fixture.Service.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest
        {
            PlanName = "Starter"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("unavailable", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Billing:Provider", response.Errors);
        Assert.Contains("Billing:StripeSecretKey", response.Errors);
    }

    [Fact]
    public async Task GetSubscriptionAsync_ExposesBillingCapabilitiesForFreeTenant()
    {
        var fixture = await CreateFixtureAsync();
        var company = await fixture.DbContext.Companies.FindAsync(fixture.CompanyId);
        company!.StripeCustomerId = null;
        await fixture.DbContext.SaveChangesAsync();

        var response = await fixture.Service.GetSubscriptionAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.False(response.Data!.HasStripeCustomer);
        Assert.False(response.Data.CanOpenBillingPortal);
        Assert.False(response.Data.CanManagePaymentMethod);
        Assert.False(response.Data.CanCancel);
        Assert.False(response.Data.CanResume);
        Assert.True(response.Data.CanStartCheckout);
    }

    [Fact]
    public async Task GetSubscriptionAsync_NewCleanTenant_ResolvesToFreeWithoutStripeCustomer()
    {
        var fixture = await CreateFixtureAsync(includeActiveSubscription: false);
        var company = await fixture.DbContext.Companies.FindAsync(fixture.CompanyId);
        company!.StripeCustomerId = null;
        await fixture.DbContext.SaveChangesAsync();

        var response = await fixture.Service.GetSubscriptionAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Free", response.Data!.PlanName);
        Assert.False(response.Data.HasStripeCustomer);
        Assert.False(response.Data.HasStripeSubscription);
    }

    [Theory]
    [InlineData("Starter", "price_starter")]
    [InlineData("Growth", "price_growth")]
    [InlineData("Scale", "price_scale")]
    public async Task CreateCheckoutSessionAsync_UsesConfiguredPriceId(string planName, string expectedPriceId)
    {
        var provider = new TestBillingProvider();
        var fixture = await CreateFixtureAsync(provider: provider);

        var response = await fixture.Service.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest
        {
            PlanName = planName,
            TrialDays = 14
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("https://checkout.stripe.test/session", response.Data!.CheckoutUrl);
        Assert.Equal(expectedPriceId, provider.LastCheckoutPriceId);
    }

    [Fact]
    public async Task GetUsageSummaryAsync_ReturnsUsageForTenant()
    {
        var fixture = await CreateFixtureAsync();

        var response = await fixture.Service.GetUsageSummaryAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(fixture.CompanyId, response.Data!.CompanyId);
        Assert.NotEmpty(response.Data.Metrics);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_RejectsWhenSubscriptionMissingProviderId()
    {
        var fixture = await CreateFixtureAsync();
        var subscription = await fixture.DbContext.TenantSubscriptions.FirstAsync();
        subscription.ExternalSubscriptionId = null;
        await fixture.DbContext.SaveChangesAsync();

        var response = await fixture.Service.CancelSubscriptionAsync(new CancelSubscriptionRequest { AtPeriodEnd = true }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("Provider subscription ID", response.Message);
    }

    [Fact]
    public async Task GetInvoicesAsync_NonAdminRole_IsRejected()
    {
        var fixture = await CreateFixtureAsync(OrganizationRole.Manager);

        var response = await fixture.Service.GetInvoicesAsync(CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("owners or administrators", response.Message);
    }

    private static async Task<Fixture> CreateFixtureAsync(
        OrganizationRole actorRole = OrganizationRole.Owner,
        bool includeActiveSubscription = true,
        BillingOptions? configuredOptions = null,
        IBillingProvider? provider = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"billing-service-{Guid.NewGuid():N}")
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var freePlanId = Guid.NewGuid();
        var starterPlanId = Guid.NewGuid();
        var growthPlanId = Guid.NewGuid();
        var scalePlanId = Guid.NewGuid();

        var tenantContext = new FakeTenantContext(companyId, userId, true);
        var dbContext = new ApplicationDbContext(options, tenantContext);

        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Billing Tenant",
            Slug = "billing-tenant",
            NormalizedSlug = "BILLING-TENANT",
            Status = "Active",
            OwnerUserId = userId,
            Email = "owner@billing.test",
            PhoneNumber = "+254700111111",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            StripeCustomerId = includeActiveSubscription ? "cus_test_123" : null,
            IsActive = true
        });

        dbContext.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            FullName = "Owner",
            Email = "owner@billing.test",
            PhoneNumber = "+254700111112",
            Role = "Owner",
            PasswordHash = "hash",
            IsActive = true
        });

        dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            Role = actorRole.ToString(),
            Status = OrganizationMemberStatus.Active.ToStorageValue(),
            JoinedAt = DateTimeOffset.UtcNow.AddDays(-10)
        });

        dbContext.SubscriptionPlans.AddRange(
        new SubscriptionPlan
        {
            Id = freePlanId,
            Name = "Free",
            DisplayName = "Free",
            Description = "Free plan",
            IsActive = true,
            SortOrder = 1
        },
        new SubscriptionPlan
        {
            Id = starterPlanId,
            Name = "Starter",
            DisplayName = "Starter",
            Description = "Starter plan",
            IsActive = true,
            SortOrder = 2
        },
        new SubscriptionPlan
        {
            Id = growthPlanId,
            Name = "Growth",
            DisplayName = "Growth",
            Description = "Growth plan",
            IsActive = true,
            SortOrder = 3
        },
        new SubscriptionPlan
        {
            Id = scalePlanId,
            Name = "Scale",
            DisplayName = "Scale",
            Description = "Scale plan",
            IsActive = true,
            SortOrder = 4
        });

        if (includeActiveSubscription)
        {
            dbContext.TenantSubscriptions.Add(new TenantSubscription
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                SubscriptionPlanId = growthPlanId,
                Status = SubscriptionStatus.Active.ToStorageValue(),
                CurrentPeriodStartUtc = DateTimeOffset.UtcNow.AddDays(-10),
                CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(20),
                ExternalSubscriptionId = "sub_test_123",
                ExternalPriceId = "price_growth"
            });
        }

        dbContext.PlanEntitlements.Add(new PlanEntitlement
        {
            Id = Guid.NewGuid(),
            SubscriptionPlanId = growthPlanId,
            Key = UsageMetric.AiRequests.ToQuotaEntitlementKey(),
            IsEnabled = true,
            QuotaLimit = 1000,
            IsUnlimited = false,
            Unit = "requests"
        });

        dbContext.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Metric = UsageMetric.AiRequests.ToStorageValue(),
            PeriodStartUtc = DateTimeOffset.UtcNow.AddDays(-5),
            PeriodEndUtc = DateTimeOffset.UtcNow.AddDays(25),
            QuantityUsed = 120
        });

        await dbContext.SaveChangesAsync();

        var billingOptionsValue = configuredOptions ?? new BillingOptions
        {
            Provider = "Stripe",
            StripeSecretKey = "sk_test_123",
            StripeWebhookSigningSecret = "whsec_test",
            CheckoutSuccessUrl = "https://example.test/success",
            CheckoutCancelUrl = "https://example.test/cancel",
            BillingPortalReturnUrl = "https://example.test/portal",
            PlanPriceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Growth"] = "price_growth",
                ["Starter"] = "price_starter",
                ["Scale"] = "price_scale"
            }
        };

        var billingOptions = Options.Create(billingOptionsValue);

        var billingProvider = provider ?? (billingOptionsValue.Provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase)
            ? new TestBillingProvider()
            : new DevelopmentBillingProvider(billingOptions));
        var entitlementService = new SubscriptionEntitlementService(dbContext, NullLogger<SubscriptionEntitlementService>.Instance);

        var service = new BillingService(
            dbContext,
            tenantContext,
            entitlementService,
            billingProvider,
            billingOptions,
            NullLogger<BillingService>.Instance);

        var payload = "{" +
            "\"id\":\"evt_test_1\"," +
            "\"type\":\"invoice.paid\"," +
            "\"created\":1730000000," +
            "\"data\":{\"object\":{" +
            "\"id\":\"in_test_1\"," +
            "\"customer\":\"cus_test_123\"," +
            "\"subscription\":\"sub_test_123\"," +
            "\"status\":\"paid\"," +
            "\"amount_due\":2500," +
            "\"amount_paid\":2500," +
            "\"currency\":\"usd\"}}}";

        return new Fixture(service, dbContext, companyId, payload, "test-signature");
    }

    private sealed record Fixture(
        BillingService Service,
        ApplicationDbContext DbContext,
        Guid CompanyId,
        string RawWebhookPayload,
        string ValidSignatureHeader);

    private sealed class FakeTenantContext(Guid? companyId, Guid? userId, bool isAuthenticated) : ICurrentTenantContext, ITenantContext
    {
        public Guid? TenantId => companyId;
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId => "corr-billing-tests";
        public bool IsAuthenticated { get; } = isAuthenticated;
    }

    private sealed class TestBillingProvider : IBillingProvider
    {
        public string ProviderName => "Stripe";

        public string? LastCheckoutPriceId { get; private set; }

        public Task<string> EnsureCustomerAsync(BillingCustomerRequest request, CancellationToken cancellationToken)
            => Task.FromResult("cus_test_checkout");

        public Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken)
        {
            LastCheckoutPriceId = request.PriceId;
            return Task.FromResult("https://checkout.stripe.test/session");
        }

        public Task<string> CreateBillingPortalSessionAsync(BillingPortalRequest request, CancellationToken cancellationToken)
            => Task.FromResult("https://billing.stripe.test/portal");

        public Task<string> CreatePaymentMethodPortalSessionAsync(BillingPortalRequest request, CancellationToken cancellationToken)
            => Task.FromResult("https://billing.stripe.test/payment-method");

        public Task<BillingProviderSubscriptionSnapshot> ChangeSubscriptionPlanAsync(ChangeSubscriptionPlanProviderRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BillingProviderSubscriptionSnapshot(
                request.SubscriptionId,
                "active",
                request.NewPriceId,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(1),
                null,
                false,
                DateTimeOffset.UtcNow));

        public Task<BillingProviderSubscriptionSnapshot> CancelSubscriptionAsync(CancelSubscriptionProviderRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BillingProviderSubscriptionSnapshot(
                request.SubscriptionId,
                request.AtPeriodEnd ? "active" : "canceled",
                "price_growth",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(1),
                null,
                request.AtPeriodEnd,
                DateTimeOffset.UtcNow));

        public Task<BillingProviderSubscriptionSnapshot> ResumeSubscriptionAsync(ResumeSubscriptionProviderRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BillingProviderSubscriptionSnapshot(
                request.SubscriptionId,
                "active",
                "price_growth",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(1),
                null,
                false,
                DateTimeOffset.UtcNow));

        public Task<BillingProviderSubscriptionSnapshot> GetSubscriptionSnapshotAsync(string subscriptionId, CancellationToken cancellationToken)
            => Task.FromResult(new BillingProviderSubscriptionSnapshot(
                subscriptionId,
                "active",
                "price_growth",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(1),
                null,
                false,
                DateTimeOffset.UtcNow));

        public BillingWebhookEnvelope ValidateAndParseWebhook(string rawBody, string signatureHeader)
        {
            _ = signatureHeader;
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            var dataObject = root.GetProperty("data").GetProperty("object").Clone();
            return new BillingWebhookEnvelope(
                root.GetProperty("id").GetString() ?? string.Empty,
                root.GetProperty("type").GetString() ?? string.Empty,
                DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("created").GetInt64()),
                dataObject.TryGetProperty("customer", out var customer) ? customer.GetString() : null,
                dataObject.TryGetProperty("subscription", out var subscription) ? subscription.GetString() : null,
                "hash",
                dataObject);
        }
    }
}
