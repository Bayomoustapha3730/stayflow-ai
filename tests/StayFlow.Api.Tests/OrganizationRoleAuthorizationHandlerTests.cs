using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Authorization;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class OrganizationRoleAuthorizationHandlerTests
{
    [Theory]
    [InlineData(OrganizationRole.Owner, OrganizationRole.Owner, true)]
    [InlineData(OrganizationRole.Administrator, OrganizationRole.Owner, false)]
    [InlineData(OrganizationRole.Manager, OrganizationRole.Support, true)]
    [InlineData(OrganizationRole.Host, OrganizationRole.Manager, false)]
    [InlineData(OrganizationRole.ReadOnly, OrganizationRole.ReadOnly, true)]
    public async Task HandleRequirementAsync_RespectsRoleHierarchy(OrganizationRole memberRole, OrganizationRole requiredRole, bool expected)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"org-auth-{Guid.NewGuid():N}")
            .Options;

        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var dbContext = new ApplicationDbContext(options);
        dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            Role = memberRole.ToString(),
            Status = OrganizationMemberStatus.Active.ToString(),
            JoinedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var handler = new OrganizationRoleAuthorizationHandler(dbContext, new FakeTenantContext(companyId, userId, true));
        var requirement = new OrganizationRoleRequirement(requiredRole);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D"))
        ], "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);

        await handler.HandleAsync(context);

        Assert.Equal(expected, context.HasSucceeded);
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