using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Organizations;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("organization/current")]
[Produces("application/json")]
public sealed class OrganizationController(IOrganizationService organizationService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> GetCurrent(CancellationToken cancellationToken)
    {
        var response = await organizationService.GetCurrentAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPut]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDto>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> UpdateCurrent(
        [FromBody] UpdateCurrentOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await organizationService.UpdateCurrentAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("members")]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<OrganizationMemberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<OrganizationMemberDto>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<OrganizationMemberDto>>>> GetCurrentMembers(CancellationToken cancellationToken)
    {
        var response = await organizationService.GetCurrentMembersAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPut("members/{memberUserId:guid}/role")]
    [Authorize(Policy = OrganizationPolicyNames.Manager)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationMemberDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationMemberDto>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<OrganizationMemberDto>>> UpdateMemberRole(
        Guid memberUserId,
        [FromBody] UpdateOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var response = await organizationService.UpdateMemberRoleAsync(memberUserId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpDelete("members/{memberUserId:guid}")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveMember(Guid memberUserId, CancellationToken cancellationToken)
    {
        var response = await organizationService.RemoveMemberAsync(memberUserId, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}