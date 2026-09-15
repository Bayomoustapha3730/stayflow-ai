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
                    ["Professional"] = "price_professional"
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
                    ["Starter"] = "price_starter"
                }
            });

        var response = await fixture.Service.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest
        {
            PlanName = "Professional"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("Professional", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Billing:PlanPriceIds", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_DevelopmentStarter_SucceedsWithoutStripeConfiguration()
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

        Assert.True(response.Success);
        Assert.Equal("Development", response.Data!.Provider);
        Assert.Contains("session_id=dev_", response.Data.CheckoutUrl, StringComparison.Ordinal);
        Assert.Contains("dev_plan_starter", response.Data.CheckoutUrl, StringComparison.Ordinal);
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
        Assert.Equal(SubscriptionPlanNames.Free, response.Data!.PlanName);
        Assert.False(response.Data.HasStripeCustomer);
        Assert.False(response.Data.HasStripeSubscription);
    }

    [Theory]
    [InlineData("Starter", "price_starter")]
    [InlineData("Professional", "price_professional")]
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
    public async Task GetPlansAsync_ReturnsCanonicalCatalog()
    {
        var fixture = await CreateFixtureAsync();

        var response = await fixture.Service.GetPlansAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(SubscriptionPlanNames.Canonical, response.Data!.Select(plan => plan.Name));
        Assert.False(response.Data.Single(plan => plan.Name == SubscriptionPlanNames.Free).IsSelfServiceCheckoutEligible);
        Assert.True(response.Data.Single(plan => plan.Name == SubscriptionPlanNames.Professional).IsSelfServiceCheckoutEligible);
        Assert.False(response.Data.Single(plan => plan.Name == SubscriptionPlanNames.Enterprise).IsSelfServiceCheckoutEligible);
    }

    [Fact]
    public async Task GetSubscriptionAsync_PersistedStarter_ReturnsCanonicalName()
    {
        var fixture = await CreateFixtureAsync();

        var response = await fixture.Service.GetSubscriptionAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(SubscriptionPlanNames.Starter, response.Data!.PlanName);
        Assert.NotEqual("Unknown", response.Data.PlanName);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_DevelopmentProfessional_SucceedsWithoutPriceMapping()
    {
        var fixture = await CreateFixtureAsync(configuredOptions: DevelopmentOptions());

        var response = await fixture.Service.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest
        {
            PlanName = SubscriptionPlanNames.Professional
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Contains("dev_plan_professional", response.Data!.CheckoutUrl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Free", "does not require checkout")]
    [InlineData("Enterprise", "sales-assisted")]
    [InlineData("NotAPlan", "was not found")]
    public async Task CreateCheckoutSessionAsync_NonSelfServicePlan_IsRejectedCleanly(string planName, string expectedMessage)
    {
        var fixture = await CreateFixtureAsync(configuredOptions: DevelopmentOptions());

        var response = await fixture.Service.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest
        {
            PlanName = planName
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains(expectedMessage, response.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains(response.Data.Metrics, metric => metric.Metric == UsageMetric.AiRequests.ToStorageValue()
            && metric.Used == 120
            && metric.Limit == 1000
            && metric.Remaining == 880);
    }

    [Fact]
    public async Task GetUsageSummaryAsync_MissingUsageRecord_ReturnsZeroUsage()
    {
        var fixture = await CreateFixtureAsync();
        fixture.DbContext.UsageRecords.RemoveRange(fixture.DbContext.UsageRecords);
        await fixture.DbContext.SaveChangesAsync();

        var response = await fixture.Service.GetUsageSummaryAsync(CancellationToken.None);

        Assert.True(response.Success);
        var metric = Assert.Single(response.Data!.Metrics);
        Assert.Equal(0, metric.Used);
        Assert.Equal(1000, metric.Remaining);
    }

    [Fact]
    public async Task GetUsageSummaryAsync_UnlimitedQuota_HasNoLimitOrRemaining()
    {
        var fixture = await CreateFixtureAsync();
        var entitlement = await fixture.DbContext.PlanEntitlements.SingleAsync();
        entitlement.IsUnlimited = true;
        entitlement.QuotaLimit = null;
        await fixture.DbContext.SaveChangesAsync();

        var response = await fixture.Service.GetUsageSummaryAsync(CancellationToken.None);

        Assert.True(response.Success);
        var metric = Assert.Single(response.Data!.Metrics);
        Assert.True(metric.IsUnlimited);
        Assert.Null(metric.Limit);
        Assert.Null(metric.Remaining);
        Assert.Equal(120, metric.Used);
    }

    [Fact]
    public async Task GetUsageSummaryAsync_DoesNotIncludeAnotherTenantsUsage()
    {
        var fixture = await CreateFixtureAsync();
        var current = await fixture.DbContext.UsageRecords.SingleAsync();
        var setupOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(fixture.DatabaseName)
            .Options;
        await using var setupContext = new ApplicationDbContext(setupOptions);
        setupContext.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            Metric = current.Metric,
            PeriodStartUtc = current.PeriodStartUtc,
            PeriodEndUtc = current.PeriodEndUtc,
            QuantityUsed = 900
        });
        await setupContext.SaveChangesAsync();

        var response = await fixture.Service.GetUsageSummaryAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(120, Assert.Single(response.Data!.Metrics).Used);
    }

    [Fact]
    public async Task GetPlansAsync_NewerCancelledSubscription_DoesNotOverrideCurrentPlan()
    {
        var fixture = await CreateFixtureAsync();
        fixture.DbContext.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            SubscriptionPlanId = fixture.EnterprisePlanId,
            Status = SubscriptionStatus.Cancelled.ToStorageValue(),
            CurrentPeriodStartUtc = DateTimeOffset.UtcNow.AddDays(1),
            CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(31)
        });
        await fixture.DbContext.SaveChangesAsync();

        var response = await fixture.Service.GetPlansAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(SubscriptionPlanNames.Starter, Assert.Single(response.Data!, plan => plan.IsCurrentPlan).Name);
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
        var databaseName = $"billing-service-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var freePlanId = Guid.NewGuid();
        var starterPlanId = Guid.NewGuid();
        var professionalPlanId = Guid.NewGuid();
        var enterprisePlanId = Guid.NewGuid();
        var periodStart = DateTimeOffset.UtcNow.AddDays(-10);
        var periodEnd = DateTimeOffset.UtcNow.AddDays(20);

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
            Name = SubscriptionPlanNames.Free,
            DisplayName = SubscriptionPlanNames.Free,
            Description = "Free plan",
            IsActive = true,
            SortOrder = 1
        },
        new SubscriptionPlan
        {
            Id = starterPlanId,
            Name = SubscriptionPlanNames.Starter,
            DisplayName = SubscriptionPlanNames.Starter,
            Description = "Starter plan",
            IsActive = true,
            SortOrder = 2
        },
        new SubscriptionPlan
        {
            Id = professionalPlanId,
            Name = SubscriptionPlanNames.Professional,
            DisplayName = SubscriptionPlanNames.Professional,
            Description = "Professional plan",
            IsActive = true,
            SortOrder = 3
        },
        new SubscriptionPlan
        {
            Id = enterprisePlanId,
            Name = SubscriptionPlanNames.Enterprise,
            DisplayName = SubscriptionPlanNames.Enterprise,
            Description = "Enterprise plan",
            IsActive = true,
            IsEnterprise = true,
            SortOrder = 4
        });

        if (includeActiveSubscription)
        {
            dbContext.TenantSubscriptions.Add(new TenantSubscription
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                SubscriptionPlanId = starterPlanId,
                Status = SubscriptionStatus.Active.ToStorageValue(),
                CurrentPeriodStartUtc = periodStart,
                CurrentPeriodEndUtc = periodEnd,
                ExternalSubscriptionId = "sub_test_123",
                ExternalPriceId = "price_starter"
            });
        }

        dbContext.PlanEntitlements.Add(new PlanEntitlement
        {
            Id = Guid.NewGuid(),
            SubscriptionPlanId = starterPlanId,
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
            PeriodStartUtc = periodStart,
            PeriodEndUtc = periodEnd,
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
                ["Starter"] = "price_starter",
                ["Professional"] = "price_professional"
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

        return new Fixture(service, dbContext, companyId, enterprisePlanId, databaseName, payload, "test-signature");
    }

    private static BillingOptions DevelopmentOptions() => new()
    {
        Provider = "Development",
        CheckoutSuccessUrl = "https://example.test/success?checkout=success",
        CheckoutCancelUrl = "https://example.test/cancel",
        BillingPortalReturnUrl = "https://example.test/portal"
    };

    private sealed record Fixture(
        BillingService Service,
        ApplicationDbContext DbContext,
        Guid CompanyId,
        Guid EnterprisePlanId,
        string DatabaseName,
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
                "price_professional",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(1),
                null,
                request.AtPeriodEnd,
                DateTimeOffset.UtcNow));

        public Task<BillingProviderSubscriptionSnapshot> ResumeSubscriptionAsync(ResumeSubscriptionProviderRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BillingProviderSubscriptionSnapshot(
                request.SubscriptionId,
                "active",
                "price_professional",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMonths(1),
                null,
                false,
                DateTimeOffset.UtcNow));

        public Task<BillingProviderSubscriptionSnapshot> GetSubscriptionSnapshotAsync(string subscriptionId, CancellationToken cancellationToken)
            => Task.FromResult(new BillingProviderSubscriptionSnapshot(
                subscriptionId,
                "active",
                "price_professional",
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
