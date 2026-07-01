using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Manages roles and role permissions.
/// </summary>
[ApiController]
[Authorize]
[Route("roles")]
[Produces("application/json")]
public sealed class RolesController(IRoleService roleService) : ControllerBase
{
    /// <summary>
    /// Gets active roles and their permissions.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RoleDto>>>> GetRoles(CancellationToken cancellationToken)
    {
        return Ok(await roleService.GetRolesAsync(cancellationToken));
    }

    /// <summary>
    /// Creates a role.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<RoleDto>>> CreateRole(
        CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var response = await roleService.CreateRoleAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Assigns a permission to a role.
    /// </summary>
    [HttpPost("{roleId:guid}/permissions")]
    public async Task<ActionResult<ApiResponse<RoleDto>>> AssignPermission(
        Guid roleId,
        AssignPermissionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await roleService.AssignPermissionAsync(roleId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
