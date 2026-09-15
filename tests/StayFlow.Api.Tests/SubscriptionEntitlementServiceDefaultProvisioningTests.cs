using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.Data;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class SubscriptionEntitlementServiceDefaultProvisioningTests
{
    [Fact]
    public async Task GetCurrentSnapshotAsync_NoSubscription_ProvisionsCanonicalFreePlan()
    {
        var harness = await CreateHarnessAsync();

        var snapshot = await harness.Service.GetCurrentSnapshotAsync(harness.CompanyId, CancellationToken.None);

        Assert.Equal(SubscriptionPlanNames.Free, snapshot.PlanName);
        Assert.Equal(harness.FreePlanId, snapshot.PlanId);
        Assert.Equal(harness.CompanyId, snapshot.CompanyId);
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_MissingFreePlan_FailsExplicitly()
    {
        var harness = await CreateHarnessAsync(includeFreePlan: false);

        var exception = await Assert.ThrowsAsync<ExternalDependencyException>(
            () => harness.Service.GetCurrentSnapshotAsync(harness.CompanyId, CancellationToken.None));

        Assert.Equal("default_plan_not_configured", exception.ErrorCode);
        Assert.Empty(harness.DbContext.TenantSubscriptions);
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_InactiveFreePlan_FailsExplicitly()
    {
        var harness = await CreateHarnessAsync(freePlanIsActive: false);

        var exception = await Assert.ThrowsAsync<ExternalDependencyException>(
            () => harness.Service.GetCurrentSnapshotAsync(harness.CompanyId, CancellationToken.None));

        Assert.Equal("default_plan_not_configured", exception.ErrorCode);
        Assert.Empty(harness.DbContext.TenantSubscriptions);
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_MissingFreePlan_DoesNotFallBackToProfessionalOrOtherActivePlan()
    {
        var harness = await CreateHarnessAsync(includeFreePlan: false);

        await Assert.ThrowsAsync<ExternalDependencyException>(
            () => harness.Service.GetCurrentSnapshotAsync(harness.CompanyId, CancellationToken.None));

        // Starter, Professional and Enterprise remain active but must never be auto-provisioned.
        Assert.True(await harness.DbContext.SubscriptionPlans.AnyAsync(plan =>
            plan.IsActive && plan.Name == SubscriptionPlanNames.Professional));
        Assert.Empty(harness.DbContext.TenantSubscriptions);
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_ExplicitlyAssignedStarter_RemainsStarter()
    {
        var harness = await CreateHarnessAsync();
        harness.DbContext.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = harness.CompanyId,
            SubscriptionPlanId = harness.StarterPlanId,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = DateTimeOffset.UtcNow.AddDays(-5),
            CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(25)
        });
        await harness.DbContext.SaveChangesAsync();

        var snapshot = await harness.Service.GetCurrentSnapshotAsync(harness.CompanyId, CancellationToken.None);

        Assert.Equal(SubscriptionPlanNames.Starter, snapshot.PlanName);
        Assert.Single(harness.DbContext.TenantSubscriptions);
    }

    private static async Task<Harness> CreateHarnessAsync(
        bool includeFreePlan = true,
        bool freePlanIsActive = true)
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var freePlanId = Guid.NewGuid();
        var starterPlanId = Guid.NewGuid();
        var professionalPlanId = Guid.NewGuid();
        var enterprisePlanId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"subscription-default-provisioning-{Guid.NewGuid():N}")
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new ApplicationDbContext(options, new FakeTenantContext(companyId, userId));

        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Provisioning Tenant",
            Slug = "provisioning-tenant",
            NormalizedSlug = "PROVISIONING-TENANT",
            Status = "Active",
            OwnerUserId = userId,
            Email = "owner@provisioning.test",
            PhoneNumber = "+254700222111",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });

        if (includeFreePlan)
        {
            dbContext.SubscriptionPlans.Add(new SubscriptionPlan
            {
                Id = freePlanId,
                Name = SubscriptionPlanNames.Free,
                DisplayName = SubscriptionPlanNames.Free,
                Description = "Free plan",
                IsActive = freePlanIsActive,
                SortOrder = 1
            });
        }

        dbContext.SubscriptionPlans.AddRange(
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

        await dbContext.SaveChangesAsync();

        var service = new SubscriptionEntitlementService(
            dbContext,
            NullLogger<SubscriptionEntitlementService>.Instance);

        return new Harness(service, dbContext, companyId, freePlanId, starterPlanId);
    }

    private sealed record Harness(
        SubscriptionEntitlementService Service,
        ApplicationDbContext DbContext,
        Guid CompanyId,
        Guid FreePlanId,
        Guid StarterPlanId);

    private sealed class FakeTenantContext(Guid? companyId, Guid? userId) : ITenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? TenantId => CompanyId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId => "corr-subscription-provisioning-tests";
        public bool IsAuthenticated => true;
    }
}
