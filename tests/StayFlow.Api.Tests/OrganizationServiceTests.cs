using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Organizations;
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

        return new Fixture(dbContext, tenantContext, actorUserId, targetUserId);
    }

    private sealed record Fixture(ApplicationDbContext DbContext, ITenantContext TenantContext, Guid OwnerUserId, Guid TargetUserId);

    private sealed class FakeTenantContext(Guid? companyId, Guid? userId, bool isAuthenticated) : ITenantContext
    {
        public Guid? TenantId => companyId;
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId => null;
        public bool IsAuthenticated { get; } = isAuthenticated;
    }
}