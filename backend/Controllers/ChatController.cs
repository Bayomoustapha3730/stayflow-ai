using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Handles authenticated tenant-scoped guest chat conversations.
/// </summary>
[ApiController]
[Route("chat")]
[Produces("application/json")]
[Authorize]
public sealed class ChatController(IChatService chatService) : ControllerBase
{
    /// <summary>
    /// Sends a guest message through the StayFlow AI orchestration pipeline.
    /// </summary>
    [HttpPost("message")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ChatMessageResponseDto>>> SendMessage(
        [FromBody] SendChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var response = await chatService.SendMessageAsync(request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    /// <summary>
    /// Gets persisted chat history for a tenant-scoped conversation.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ChatMessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ChatMessageDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ChatMessageDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PagedResult<ChatMessageDto>>>> GetHistory(
        [FromQuery] ChatHistoryQueryParameters query,
        CancellationToken cancellationToken)
    {
        var response = await chatService.GetHistoryAsync(query, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    /// <summary>
    /// Escalates a tenant-scoped chat conversation to host or support review.
    /// </summary>
    [HttpPost("escalate")]
    [ProducesResponseType(typeof(ApiResponse<ChatStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChatStatusDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ChatStatusDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ChatStatusDto>>> Escalate(
        [FromBody] EscalateChatRequest request,
        CancellationToken cancellationToken)
    {
        var response = await chatService.EscalateAsync(request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    /// <summary>
    /// Ends a tenant-scoped chat conversation.
    /// </summary>
    [HttpPost("end")]
    [ProducesResponseType(typeof(ApiResponse<ChatStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ChatStatusDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ChatStatusDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ChatStatusDto>>> End(
        [FromBody] EndChatRequest request,
        CancellationToken cancellationToken)
    {
        var response = await chatService.EndAsync(request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    private ActionResult<ApiResponse<T>> ToFailureResult<T>(ApiResponse<T> response)
    {
        return response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }
}
