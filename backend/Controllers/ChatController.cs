using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Authenticated guest-facing chat API for frontend and future channel integrations.
/// </summary>
[ApiController]
[Route("chat")]
[Produces("application/json")]
[Authorize]
public sealed class ChatController(IChatService chatService) : ControllerBase
{
    [HttpPost("message")]
    [RequiresPermission("chat.send")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ChatMessageResponse>>> SendMessage(SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        var response = await chatService.SendGuestMessageAsync(request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpGet("{conversationId:guid}")]
    [RequiresPermission("chat.read")]
    [ProducesResponseType(typeof(ApiResponse<ChatConversationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChatConversationResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ChatConversationResponse>>> GetConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        var response = await chatService.GetGuestConversationAsync(conversationId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpGet("{conversationId:guid}/history")]
    [RequiresPermission("chat.read")]
    [ProducesResponseType(typeof(ApiResponse<ChatHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChatHistoryResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ChatHistoryResponse>>> GetHistory(
        Guid conversationId,
        [FromQuery] ChatHistoryQueryParameters query,
        CancellationToken cancellationToken)
    {
        var response = await chatService.GetGuestHistoryAsync(conversationId, query, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{conversationId:guid}/escalate")]
    [RequiresPermission("chat.escalate")]
    [ProducesResponseType(typeof(ApiResponse<ChatStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChatStatusResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ChatStatusResponse>>> Escalate(
        Guid conversationId,
        EscalateChatRequest request,
        CancellationToken cancellationToken)
    {
        var response = await chatService.EscalateGuestConversationAsync(conversationId, request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{conversationId:guid}/end")]
    [RequiresPermission("chat.end")]
    [ProducesResponseType(typeof(ApiResponse<ChatStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChatStatusResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ChatStatusResponse>>> End(
        Guid conversationId,
        EndChatRequest request,
        CancellationToken cancellationToken)
    {
        var response = await chatService.EndGuestConversationAsync(conversationId, request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    private ActionResult<ApiResponse<T>> ToFailureResult<T>(ApiResponse<T> response)
    {
        return response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }
}
