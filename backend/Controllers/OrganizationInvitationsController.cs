using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Organizations;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/organization/invitations")]
[Produces("application/json")]
public sealed class OrganizationInvitationsController(IOrganizationInvitationService invitationService) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<CreatedOrganizationInvitationDto>>> Create(
        [FromBody] CreateOrganizationInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await invitationService.CreateAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<OrganizationInvitationDto>>>> List(CancellationToken cancellationToken)
    {
        var response = await invitationService.ListAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var response = await invitationService.RevokeAsync(id, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("{id:guid}/resend")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<ResentOrganizationInvitationDto>>> Resend(Guid id, CancellationToken cancellationToken)
    {
        var response = await invitationService.ResendAsync(id, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("accept")]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    public async Task<ActionResult<ApiResponse<object>>> Accept([FromBody] AcceptOrganizationInvitationRequest request, CancellationToken cancellationToken)
    {
        var response = await invitationService.AcceptAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [AllowAnonymous]
    [HttpPost("reject")]
    public async Task<ActionResult<ApiResponse<object>>> Reject([FromBody] RejectOrganizationInvitationRequest request, CancellationToken cancellationToken)
    {
        var response = await invitationService.RejectAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}