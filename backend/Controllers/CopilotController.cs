using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Copilot;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Route("copilot/conversations")]
[Produces("application/json")]
[Authorize]
public sealed class CopilotController(ICopilotService copilotService) : ControllerBase
{
    [HttpPost("{conversationId:guid}/suggest-reply")]
    [RequiresPermission("conversations.reply")]
    [ProducesResponseType(
        typeof(ApiResponse<CopilotSuggestReplyResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<CopilotSuggestReplyResponse>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<CopilotSuggestReplyResponse>),
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CopilotSuggestReplyResponse>>> SuggestReply(
        Guid conversationId,
        [FromBody] CopilotSuggestReplyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await copilotService.SuggestHostReplyAsync(
            conversationId,
            request,
            cancellationToken);

        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpGet("{conversationId:guid}/summary")]
    [RequiresPermission("conversations.read")]
    [ProducesResponseType(
        typeof(ApiResponse<ConversationCopilotSummaryResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<ConversationCopilotSummaryResponse>),
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ConversationCopilotSummaryResponse>>> GetSummary(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var response = await copilotService.GetSummaryAsync(
            conversationId,
            cancellationToken);

        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpGet("{conversationId:guid}/suggested-replies")]
    [RequiresPermission("conversations.read")]
    [ProducesResponseType(
        typeof(ApiResponse<ConversationCopilotSuggestionsResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<ConversationCopilotSuggestionsResponse>),
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ConversationCopilotSuggestionsResponse>>> GetSuggestedReplies(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var response = await copilotService.GetSuggestedRepliesAsync(
            conversationId,
            cancellationToken);

        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    private ActionResult<ApiResponse<T>> ToFailureResult<T>(
        ApiResponse<T> response)
    {
        return response.Errors.Count > 0
            ? BadRequest(response)
            : NotFound(response);
    }
}