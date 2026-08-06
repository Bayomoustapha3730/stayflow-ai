using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Authorization;

public sealed class OrganizationRoleAuthorizationHandler(
    ApplicationDbContext dbContext,
    ITenantContext tenantContext) : AuthorizationHandler<OrganizationRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (tenantContext.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty)
        {
            return;
        }

        var roleValues = await dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.CompanyId == companyId
                && member.UserId == userId
                && member.Status == OrganizationMemberStatus.Active.ToStorageValue())
            .Select(member => member.Role)
            .ToListAsync();

        if (roleValues.Count == 0)
        {
            return;
        }

        var maxRole = OrganizationRole.ReadOnly;
        foreach (var roleValue in roleValues)
        {
            if (OrganizationRoleExtensions.TryParse(roleValue, out var parsedRole) && parsedRole > maxRole)
            {
                maxRole = parsedRole;
            }
        }

        if (maxRole >= requirement.MinimumRole)
        {
            context.Succeed(requirement);
        }
    }
}