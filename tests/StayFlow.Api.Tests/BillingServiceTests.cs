using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var fixture = await CreateFixtureAsync();

        var first = await fixture.Service.ProcessStripeWebhookAsync(fixture.RawWebhookPayload, fixture.ValidSignatureHeader, CancellationToken.None);
        var second = await fixture.Service.ProcessStripeWebhookAsync(fixture.RawWebhookPayload, fixture.ValidSignatureHeader, CancellationToken.None);

        Assert.False(first.WasDuplicate);
        Assert.True(second.WasDuplicate);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ReturnsActionableError_WhenPriceMappingMissing()
    {
        var fixture = await CreateFixtureAsync();

        var response = await fixture.Service.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest
        {
            PlanName = "Scale"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("Scale", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Billing:PlanPriceIds", response.Message, StringComparison.OrdinalIgnoreCase);
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

    private static async Task<Fixture> CreateFixtureAsync(OrganizationRole actorRole = OrganizationRole.Owner)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"billing-service-{Guid.NewGuid():N}")
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();

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
            StripeCustomerId = "cus_test_123",
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

        dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = "Growth",
            DisplayName = "Growth",
            Description = "Growth plan",
            IsActive = true,
            SortOrder = 1
        });

        dbContext.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = DateTimeOffset.UtcNow.AddDays(-10),
            CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(20),
            ExternalSubscriptionId = "sub_test_123",
            ExternalPriceId = "price_growth"
        });

        dbContext.PlanEntitlements.Add(new PlanEntitlement
        {
            Id = Guid.NewGuid(),
            SubscriptionPlanId = planId,
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

        var billingOptions = Options.Create(new BillingOptions
        {
            Provider = "Development",
            StripeWebhookSigningSecret = "whsec_test",
            PlanPriceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Growth"] = "price_growth",
                ["Starter"] = "price_starter"
            }
        });

        var provider = new DevelopmentBillingProvider(billingOptions);
        var entitlementService = new SubscriptionEntitlementService(dbContext, NullLogger<SubscriptionEntitlementService>.Instance);

        var service = new BillingService(
            dbContext,
            tenantContext,
            entitlementService,
            provider,
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
}
