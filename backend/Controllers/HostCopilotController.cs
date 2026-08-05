using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.Copilot;
using StayFlow.Api.Services.ConciergeActions;
using StayFlow.Api.Services.HostCopilot;

namespace StayFlow.Api.Controllers;

[ApiController]
[Route("host/copilot")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("host-api")]
public sealed class HostCopilotController(IHostCopilotWorkspaceService workspaceService) : ControllerBase
{
    [HttpGet("workspace")]
    [RequiresPermission("conversations.read")]
    [ProducesResponseType(typeof(ApiResponse<HostCopilotWorkspaceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HostCopilotWorkspaceResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<HostCopilotWorkspaceResponse>>> GetWorkspace(
        [FromQuery] Guid? propertyId,
        CancellationToken cancellationToken)
    {
        var response = await workspaceService.GetWorkspaceAsync(propertyId, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("conversations/{conversationId:guid}/draft")]
    [RequiresPermission("conversations.reply")]
    [ProducesResponseType(typeof(ApiResponse<HostCopilotDraftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HostCopilotDraftResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<HostCopilotDraftResponse>>> GenerateDraft(
        Guid conversationId,
        [FromBody] HostCopilotDraftGenerateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await workspaceService.GenerateDraftAsync(conversationId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("conversations/{conversationId:guid}/draft/validate")]
    [RequiresPermission("conversations.reply")]
    [ProducesResponseType(typeof(ApiResponse<HostCopilotDraftValidationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HostCopilotDraftValidationResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<HostCopilotDraftValidationResponse>>> ValidateDraft(
        Guid conversationId,
        [FromBody] HostCopilotDraftValidateRequest request,
        CancellationToken cancellationToken)
    {
        var response = await workspaceService.ValidateDraftAsync(conversationId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("conversations/{conversationId:guid}/draft/send")]
    [RequiresPermission("conversations.reply")]
    [ProducesResponseType(typeof(ApiResponse<ConversationMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConversationMessageResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ConversationMessageResponse>>> SendDraft(
        Guid conversationId,
        [FromBody] HostCopilotDraftSendRequest request,
        CancellationToken cancellationToken)
    {
        var response = await workspaceService.SendDraftAsync(conversationId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("actions/{actionId:guid}/approve")]
    [RequiresPermission("conversations.manage")]
    [ProducesResponseType(typeof(ApiResponse<HostActionListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HostActionListItem>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<HostActionListItem>>> ApproveAction(
        Guid actionId,
        [FromBody] HostActionDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await workspaceService.ApprovePendingActionAsync(actionId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("actions/{actionId:guid}/decline")]
    [RequiresPermission("conversations.manage")]
    [ProducesResponseType(typeof(ApiResponse<HostActionListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HostActionListItem>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<HostActionListItem>>> DeclineAction(
        Guid actionId,
        [FromBody] HostActionDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await workspaceService.DeclinePendingActionAsync(actionId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
