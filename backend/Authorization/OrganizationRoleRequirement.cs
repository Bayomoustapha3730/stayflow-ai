using Microsoft.AspNetCore.Authorization;
using StayFlow.Api.Models;

namespace StayFlow.Api.Authorization;

public sealed class OrganizationRoleRequirement(OrganizationRole minimumRole) : IAuthorizationRequirement
{
    public OrganizationRole MinimumRole { get; } = minimumRole;
}