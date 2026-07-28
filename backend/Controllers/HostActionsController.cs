using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Controllers;

[ApiController]
[Route("host/actions")]
[Produces("application/json")]
[Authorize]
public sealed class HostActionsController(
    ICurrentTenantContext tenantContext,
    IConciergeHostActionService hostActionService) : ControllerBase
{
    [HttpGet]
    [RequiresPermission("conversations.read")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<HostActionListItem>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<HostActionListItem>>>> List(
        [FromQuery] Guid? propertyId,
        [FromQuery] PendingConciergeActionStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsAuthenticated || tenantContext.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return BadRequest(ApiResponse<PagedResult<HostActionListItem>>.Fail("Authenticated tenant context is required."));
        }

        var result = await hostActionService.ListAsync(companyId, propertyId, status, page, pageSize, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{actionId:guid}/approve")]
    [RequiresPermission("conversations.manage")]
    [ProducesResponseType(typeof(ApiResponse<HostActionListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HostActionListItem>>> Approve(
        Guid actionId,
        HostActionDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return BadRequest(ApiResponse<HostActionListItem>.Fail(error));
        }

        var result = await hostActionService.ApproveAsync(companyId, actionId, userId, request.DecisionNote, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{actionId:guid}/decline")]
    [RequiresPermission("conversations.manage")]
    [ProducesResponseType(typeof(ApiResponse<HostActionListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HostActionListItem>>> Decline(
        Guid actionId,
        HostActionDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return BadRequest(ApiResponse<HostActionListItem>.Fail(error));
        }

        var result = await hostActionService.DeclineAsync(companyId, actionId, userId, request.DecisionNote, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private bool TryGetContext(out Guid companyId, out Guid userId, out string error)
    {
        if (!tenantContext.IsAuthenticated || tenantContext.CompanyId is not { } tenantCompanyId || tenantCompanyId == Guid.Empty)
        {
            companyId = Guid.Empty;
            userId = Guid.Empty;
            error = "Authenticated tenant context is required.";
            return false;
        }

        if (tenantContext.UserId is not { } tenantUserId || tenantUserId == Guid.Empty)
        {
            companyId = Guid.Empty;
            userId = Guid.Empty;
            error = "Authenticated user context is required.";
            return false;
        }

        companyId = tenantCompanyId;
        userId = tenantUserId;
        error = string.Empty;
        return true;
    }
}
