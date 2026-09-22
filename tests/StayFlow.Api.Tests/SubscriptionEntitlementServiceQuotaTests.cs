using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.Data;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class SubscriptionEntitlementServiceQuotaTests
{
    [Fact]
    public async Task RetryableFailureThreeTimes_StopsAfterThirdAttemptWithSafeApplicationError()
    {
        var attempts = 0;
        var resets = 0;
        var exception = await Assert.ThrowsAsync<ExternalDependencyException>(() =>
            QuotaAdmissionRetryPolicy.ExecuteAsync<int>(
                _ =>
                {
                    attempts++;
                    throw new InvalidOperationException("retryable test failure");
                },
                () =>
                {
                    resets++;
                    return Task.CompletedTask;
                },
                _ => true,
                CancellationToken.None));

        Assert.Equal(3, attempts);
        Assert.Equal(3, resets);
        Assert.Equal("quota_admission_unavailable", exception.ErrorCode);
        Assert.DoesNotContain("retryable test failure", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExistingOperationAtExhaustedQuota_IsIdempotentButNewOperationIsRejected()
    {
        var harness = await CreateHarnessAsync(limit: 1);

        await harness.Service.ConsumeQuotaAsync(harness.CompanyId, UsageMetric.WhatsAppMessages, 1, "operation-a", CancellationToken.None);

        var replay = await harness.Service.ConsumeQuotaAsync(harness.CompanyId, UsageMetric.WhatsAppMessages, 1, "operation-a", CancellationToken.None);
        var exception = await Assert.ThrowsAsync<QuotaExceededException>(() =>
            harness.Service.ConsumeQuotaAsync(harness.CompanyId, UsageMetric.WhatsAppMessages, 1, "operation-b", CancellationToken.None));

        Assert.True(replay.WasIdempotentReplay);
        Assert.Equal(1, exception.Current);
        Assert.Equal(1, (await harness.DbContext.UsageRecords.SingleAsync()).QuantityUsed);
    }

    [Fact]
    public async Task PreviousBillingPeriod_DoesNotConsumeCurrentAllowance()
    {
        var harness = await CreateHarnessAsync(limit: 1);
        harness.DbContext.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = harness.CompanyId,
            Metric = UsageMetric.WhatsAppMessages.ToStorageValue(),
            PeriodStartUtc = harness.PeriodStartUtc.AddMonths(-1),
            PeriodEndUtc = harness.PeriodEndUtc.AddMonths(-1),
            QuantityUsed = 1
        });
        harness.DbContext.UsageOperations.Add(new UsageOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = harness.CompanyId,
            Metric = UsageMetric.WhatsAppMessages.ToStorageValue(),
            PeriodStartUtc = harness.PeriodStartUtc.AddMonths(-1),
            IdempotencyKey = "previous-period-operation",
            Quantity = 1
        });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.ConsumeQuotaAsync(
            harness.CompanyId, UsageMetric.WhatsAppMessages, 1, "current-period-operation", CancellationToken.None);

        Assert.False(result.WasIdempotentReplay);
        Assert.Equal(1, result.UpdatedUsage);
        Assert.Equal(2, await harness.DbContext.UsageRecords.CountAsync());
    }

    [Fact]
    public async Task TenantUsageAndOperations_AreIsolated()
    {
        var first = await CreateHarnessAsync(limit: 1);
        var second = await CreateHarnessAsync(limit: 1, databaseName: first.DatabaseName);

        await first.Service.ConsumeQuotaAsync(first.CompanyId, UsageMetric.WhatsAppMessages, 1, "same-key", CancellationToken.None);
        var result = await second.Service.ConsumeQuotaAsync(second.CompanyId, UsageMetric.WhatsAppMessages, 1, "same-key", CancellationToken.None);

        Assert.False(result.WasIdempotentReplay);
        Assert.Equal(1, result.UpdatedUsage);
    }

    [Fact]
    public async Task EnterpriseUnlimited_AdmitsUsageBeyondFiniteLimits()
    {
        var harness = await CreateHarnessAsync(limit: null, unlimited: true);

        var result = await harness.Service.ConsumeQuotaAsync(
            harness.CompanyId, UsageMetric.WhatsAppMessages, 100, "enterprise-operation", CancellationToken.None);

        Assert.True(result.IsUnlimited);
        Assert.Equal(100, result.UpdatedUsage);
    }

    [Fact]
    public async Task CurrentSnapshot_UsesActiveResourcesAndIgnoresHistoricalResourceUsage()
    {
        var harness = await CreateResourceHarnessAsync();

        var snapshot = await harness.Service.GetCurrentSnapshotAsync(harness.CompanyId, CancellationToken.None);
        var users = Assert.Single(snapshot.Quotas.Where(quota => quota.Metric == UsageMetric.Users));
        var properties = Assert.Single(snapshot.Quotas.Where(quota => quota.Metric == UsageMetric.Properties));

        Assert.Equal(2, users.Used);
        Assert.Equal(1, users.Remaining);
        Assert.Equal(1, properties.Used);
        Assert.Equal(1, properties.Remaining);
    }

    [Fact]
    public async Task CurrentSnapshot_WhenCurrentCountExceedsDowngradedLimit_ClampsRemainingToZero()
    {
        var harness = await CreateResourceHarnessAsync();
        var propertyEntitlement = await harness.DbContext.PlanEntitlements
            .SingleAsync(entitlement => entitlement.Key == UsageMetric.Properties.ToQuotaEntitlementKey());
        propertyEntitlement.QuotaLimit = 0;
        await harness.DbContext.SaveChangesAsync();

        var snapshot = await harness.Service.GetCurrentSnapshotAsync(harness.CompanyId, CancellationToken.None);
        var properties = Assert.Single(snapshot.Quotas.Where(quota => quota.Metric == UsageMetric.Properties));

        Assert.Equal(1, properties.Used);
        Assert.Equal(0, properties.Remaining);

        var capacityService = new ResourceCapacityService(harness.DbContext, harness.Service);
        await Assert.ThrowsAsync<QuotaExceededException>(() =>
            capacityService.EnsureCapacityAsync(harness.CompanyId, UsageMetric.Properties, CancellationToken.None));
    }

    [Fact]
    public async Task FiniteLimit_RejectsNewOperationAfterLimitIsReached()
    {
        var harness = await CreateHarnessAsync(limit: 1);

        await harness.Service.ConsumeQuotaAsync(harness.CompanyId, UsageMetric.WhatsAppMessages, 1, "first-operation", CancellationToken.None);

        await Assert.ThrowsAsync<QuotaExceededException>(() =>
            harness.Service.ConsumeQuotaAsync(harness.CompanyId, UsageMetric.WhatsAppMessages, 1, "second-operation", CancellationToken.None));
    }

    private static async Task<Harness> CreateHarnessAsync(
        long? limit,
        bool unlimited = false,
        string? databaseName = null)
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var periodStartUtc = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEndUtc = periodStartUtc.AddMonths(1).AddTicks(-1);
        var resolvedDatabaseName = databaseName ?? $"quota-tests-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(resolvedDatabaseName)
            .Options;
        var dbContext = new ApplicationDbContext(options, new FakeTenantContext(companyId, userId));

        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Name = $"Quota Tenant {companyId:N}",
            Slug = $"quota-{companyId:N}",
            NormalizedSlug = $"QUOTA-{companyId:N}".ToUpperInvariant(),
            Status = "Active",
            OwnerUserId = null,
            Email = $"{companyId:N}@quota.test",
            PhoneNumber = "+254700000000",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = $"QuotaTest-{planId:N}",
            DisplayName = $"QuotaTest-{planId:N}",
            Description = "Quota test plan",
            IsActive = true,
            IsEnterprise = unlimited,
            SortOrder = 1,
            Entitlements =
            [
                new PlanEntitlement
                {
                    Id = Guid.NewGuid(),
                    Key = "Quota.WhatsAppMessages",
                    IsEnabled = true,
                    QuotaLimit = limit,
                    IsUnlimited = unlimited
                }
            ]
        });
        dbContext.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = periodStartUtc,
            CurrentPeriodEndUtc = periodEndUtc
        });
        await dbContext.SaveChangesAsync();

        return new Harness(
            new SubscriptionEntitlementService(dbContext, NullLogger<SubscriptionEntitlementService>.Instance),
            dbContext,
            companyId,
            periodStartUtc,
            periodEndUtc,
            resolvedDatabaseName);
    }

    private static async Task<ResourceHarness> CreateResourceHarnessAsync()
    {
        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var periodStartUtc = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEndUtc = periodStartUtc.AddMonths(1).AddTicks(-1);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"resource-snapshot-tests-{Guid.NewGuid():N}")
            .Options;
        var dbContext = new ApplicationDbContext(options, new FakeTenantContext(companyId, Guid.NewGuid()));

        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Resource Snapshot Tenant",
            Slug = $"resource-{companyId:N}",
            NormalizedSlug = $"RESOURCE-{companyId:N}",
            Status = "Active",
            Email = $"{companyId:N}@resource.test",
            PhoneNumber = "+254700000000",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = "ResourceTest",
            DisplayName = "ResourceTest",
            Description = "Resource snapshot test plan",
            IsActive = true,
            Entitlements =
            [
                new PlanEntitlement { Id = Guid.NewGuid(), Key = UsageMetric.Users.ToQuotaEntitlementKey(), IsEnabled = true, QuotaLimit = 3, Unit = "seats" },
                new PlanEntitlement { Id = Guid.NewGuid(), Key = UsageMetric.Properties.ToQuotaEntitlementKey(), IsEnabled = true, QuotaLimit = 2, Unit = "properties" },
                new PlanEntitlement { Id = Guid.NewGuid(), Key = UsageMetric.AiRequests.ToQuotaEntitlementKey(), IsEnabled = true, QuotaLimit = 100, Unit = "requests" }
            ]
        });
        dbContext.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = periodStartUtc,
            CurrentPeriodEndUtc = periodEndUtc
        });
        dbContext.OrganizationMembers.AddRange(
            new OrganizationMember { Id = Guid.NewGuid(), CompanyId = companyId, UserId = Guid.NewGuid(), Status = OrganizationMemberStatus.Active.ToStorageValue() },
            new OrganizationMember { Id = Guid.NewGuid(), CompanyId = companyId, UserId = Guid.NewGuid(), Status = OrganizationMemberStatus.Active.ToStorageValue() },
            new OrganizationMember { Id = Guid.NewGuid(), CompanyId = companyId, UserId = Guid.NewGuid(), Status = OrganizationMemberStatus.Removed.ToStorageValue() });
        dbContext.Properties.AddRange(
            new Property { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Active", IsDeleted = false },
            new Property { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Deleted", IsDeleted = true });
        dbContext.UsageRecords.AddRange(
            new UsageRecord { Id = Guid.NewGuid(), CompanyId = companyId, Metric = UsageMetric.Users.ToStorageValue(), PeriodStartUtc = periodStartUtc, PeriodEndUtc = periodEndUtc, QuantityUsed = 99 },
            new UsageRecord { Id = Guid.NewGuid(), CompanyId = companyId, Metric = UsageMetric.Properties.ToStorageValue(), PeriodStartUtc = periodStartUtc, PeriodEndUtc = periodEndUtc, QuantityUsed = 99 });
        await dbContext.SaveChangesAsync();

        return new ResourceHarness(
            new SubscriptionEntitlementService(dbContext, NullLogger<SubscriptionEntitlementService>.Instance),
            dbContext,
            companyId);
    }

    private sealed record Harness(
        SubscriptionEntitlementService Service,
        ApplicationDbContext DbContext,
        Guid CompanyId,
        DateTimeOffset PeriodStartUtc,
        DateTimeOffset PeriodEndUtc,
        string DatabaseName);

    private sealed record ResourceHarness(
        SubscriptionEntitlementService Service,
        ApplicationDbContext DbContext,
        Guid CompanyId);

    private sealed class FakeTenantContext(Guid? companyId, Guid? userId) : ITenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? TenantId => CompanyId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId => "quota-test";
        public bool IsAuthenticated => true;
    }

}
