using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Organizations;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class OrganizationServiceTests
{
    [Fact]
    public async Task RemoveMemberAsync_DoesNotRemoveSoleOwner()
    {
        var fixture = await CreateFixtureAsync();
        var service = new OrganizationService(fixture.DbContext, fixture.TenantContext);

        var response = await service.RemoveMemberAsync(fixture.OwnerUserId, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("The sole organization owner cannot be removed.", response.Message);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_ManagerCannotPromoteAdministrator()
    {
        var fixture = await CreateFixtureAsync(actorRole: OrganizationRole.Manager, targetRole: OrganizationRole.Host);
        var service = new OrganizationService(fixture.DbContext, fixture.TenantContext);

        var response = await service.UpdateMemberRoleAsync(
            fixture.TargetUserId,
            new UpdateOrganizationMemberRoleRequest { Role = "Administrator" },
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("You are not allowed to change this member role.", response.Message);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_OwnerCanPromoteMember()
    {
        var fixture = await CreateFixtureAsync(actorRole: OrganizationRole.Owner, targetRole: OrganizationRole.ReadOnly);
        var service = new OrganizationService(fixture.DbContext, fixture.TenantContext);

        var response = await service.UpdateMemberRoleAsync(
            fixture.TargetUserId,
            new UpdateOrganizationMemberRoleRequest { Role = "Manager" },
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Manager", response.Data?.Role);
    }

    [Fact]
    public async Task RemoveMemberAsync_RestoresCapacityForAnotherAdmission()
    {
        var fixture = await CreateFixtureAsync();
        var organizationService = new OrganizationService(fixture.DbContext, fixture.TenantContext);
        var removed = await organizationService.RemoveMemberAsync(fixture.TargetUserId, CancellationToken.None);

        Assert.True(removed.Success);

        var capacityService = new ResourceCapacityService(
            fixture.DbContext,
            new FixedCapacityEntitlementService(fixture.CompanyId, UsageMetric.Users, 2));

        await capacityService.EnsureCapacityAsync(fixture.CompanyId, UsageMetric.Users, CancellationToken.None);
        Assert.Equal(OrganizationMemberStatus.Removed.ToStorageValue(),
            (await fixture.DbContext.OrganizationMembers.SingleAsync(member => member.UserId == fixture.TargetUserId)).Status);
    }

    private static async Task<Fixture> CreateFixtureAsync(
        OrganizationRole actorRole = OrganizationRole.Owner,
        OrganizationRole targetRole = OrganizationRole.ReadOnly)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"org-service-{Guid.NewGuid():N}")
            .Options;

        var companyId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var tenantContext = new FakeTenantContext(companyId, actorUserId, true);
        var dbContext = new ApplicationDbContext(options, tenantContext);

        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Tenant",
            Slug = "tenant",
            NormalizedSlug = "TENANT",
            Status = "Active",
            OwnerUserId = actorUserId,
            Email = "tenant@example.com",
            PhoneNumber = "+254700000100",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });

        dbContext.Users.AddRange(
            new User
            {
                Id = actorUserId,
                CompanyId = companyId,
                FullName = "Owner",
                Email = "owner@example.com",
                PhoneNumber = "+254700000101",
                Role = "Owner",
                PasswordHash = "hash",
                IsActive = true
            },
            new User
            {
                Id = targetUserId,
                CompanyId = companyId,
                FullName = "Target",
                Email = "target@example.com",
                PhoneNumber = "+254700000102",
                Role = "Host",
                PasswordHash = "hash",
                IsActive = true
            });

        dbContext.OrganizationMembers.AddRange(
            new OrganizationMember
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = actorUserId,
                Role = actorRole.ToString(),
                Status = OrganizationMemberStatus.Active.ToString(),
                JoinedAt = DateTimeOffset.UtcNow.AddDays(-10)
            },
            new OrganizationMember
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = targetUserId,
                Role = targetRole.ToString(),
                Status = OrganizationMemberStatus.Active.ToString(),
                JoinedAt = DateTimeOffset.UtcNow.AddDays(-8)
            });

        await dbContext.SaveChangesAsync();

        return new Fixture(dbContext, tenantContext, companyId, actorUserId, targetUserId);
    }

    private sealed record Fixture(ApplicationDbContext DbContext, ITenantContext TenantContext, Guid CompanyId, Guid OwnerUserId, Guid TargetUserId);

    private sealed class FixedCapacityEntitlementService(Guid companyId, UsageMetric metric, long limit) : ISubscriptionEntitlementService
    {
        public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid requestedCompanyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SubscriptionSnapshot(
                companyId,
                Guid.Empty,
                Guid.Empty,
                "Test",
                "Test",
                SubscriptionStatus.Active.ToStorageValue(),
                false,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(30),
                [],
                [new QuotaSnapshot(metric, metric.ToQuotaEntitlementKey(), limit, 0, limit, false, "count", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30))]));
        }

        public Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid requestedCompanyId, CancellationToken cancellationToken)
            => GetCurrentSnapshotAsync(requestedCompanyId, cancellationToken).ContinueWith(task => (SubscriptionSnapshot?)task.Result, cancellationToken);

        public Task EnsureFeatureEnabledAsync(Guid requestedCompanyId, string featureKey, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<UsageConsumptionResult> ConsumeQuotaAsync(Guid requestedCompanyId, UsageMetric requestedMetric, long quantity, string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SubscriptionSnapshot> UpdatePlanAsync(Guid requestedCompanyId, Guid? planId, string? planName, string? notes, CancellationToken cancellationToken) => GetCurrentSnapshotAsync(requestedCompanyId, cancellationToken);
    }

    private sealed class FakeTenantContext(Guid? companyId, Guid? userId, bool isAuthenticated) : ITenantContext
    {
        public Guid? TenantId => companyId;
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId => null;
        public bool IsAuthenticated { get; } = isAuthenticated;
    }
}