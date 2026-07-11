using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Staff API for tenant-scoped guest conversation management.
/// </summary>
[ApiController]
[Route("conversations")]
[Produces("application/json")]
[Authorize]
public sealed class ConversationsController(IConversationService conversationService) : ControllerBase
{
    [HttpPost]
    [RequiresPermission("conversations.create")]
    [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ConversationDetailResponse>>> CreateConversation(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var response = await conversationService.CreateOrGetConversationAsync(request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpGet("{conversationId:guid}")]
    [RequiresPermission("conversations.read")]
    [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ConversationDetailResponse>>> GetConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        var response = await conversationService.GetConversationAsync(conversationId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpGet("{conversationId:guid}/messages")]
    [RequiresPermission("conversations.read")]
    [ProducesResponseType(typeof(ApiResponse<ConversationHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConversationHistoryResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ConversationHistoryResponse>>> GetMessages(
        Guid conversationId,
        [FromQuery] ConversationHistoryQueryParameters query,
        CancellationToken cancellationToken)
    {
        var response = await conversationService.GetConversationHistoryAsync(conversationId, query, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{conversationId:guid}/messages/host")]
    [RequiresPermission("conversations.reply")]
    [ProducesResponseType(typeof(ApiResponse<ConversationMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConversationMessageResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ConversationMessageResponse>>> AddHostMessage(
        Guid conversationId,
        AddHostMessageRequest request,
        CancellationToken cancellationToken)
    {
        var response = await conversationService.AddHostMessageAsync(conversationId, request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{conversationId:guid}/notes")]
    [RequiresPermission("conversations.notes")]
    [ProducesResponseType(typeof(ApiResponse<ConversationMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConversationMessageResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ConversationMessageResponse>>> AddInternalNote(
        Guid conversationId,
        AddInternalNoteRequest request,
        CancellationToken cancellationToken)
    {
        var response = await conversationService.AddInternalNoteAsync(conversationId, request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{conversationId:guid}/escalate")]
    [RequiresPermission("conversations.escalate")]
    [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ConversationDetailResponse>>> Escalate(
        Guid conversationId,
        EscalateConversationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await conversationService.EscalateConversationAsync(conversationId, request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{conversationId:guid}/human-takeover")]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<ConversationDetailResponse>>> EnableHumanTakeover(Guid conversationId, CancellationToken cancellationToken)
    {
        var response = await conversationService.EnableHumanTakeoverAsync(conversationId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{conversationId:guid}/return-to-ai")]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<ConversationDetailResponse>>> ReturnToAI(Guid conversationId, CancellationToken cancellationToken)
    {
        var response = await conversationService.ReturnToAIModeAsync(conversationId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{conversationId:guid}/resolve")]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<ConversationDetailResponse>>> Resolve(Guid conversationId, CancellationToken cancellationToken)
    {
        var response = await conversationService.ResolveConversationAsync(conversationId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{conversationId:guid}/close")]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<ConversationDetailResponse>>> Close(Guid conversationId, CancellationToken cancellationToken)
    {
        var response = await conversationService.CloseConversationAsync(conversationId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    private ActionResult<ApiResponse<T>> ToFailureResult<T>(ApiResponse<T> response)
    {
        return response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }
}
