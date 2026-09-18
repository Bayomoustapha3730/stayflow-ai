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

    private sealed record Harness(
        SubscriptionEntitlementService Service,
        ApplicationDbContext DbContext,
        Guid CompanyId,
        DateTimeOffset PeriodStartUtc,
        DateTimeOffset PeriodEndUtc,
        string DatabaseName);

    private sealed class FakeTenantContext(Guid? companyId, Guid? userId) : ITenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? TenantId => CompanyId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId => "quota-test";
        public bool IsAuthenticated => true;
    }

}
