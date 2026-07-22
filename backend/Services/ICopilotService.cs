using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Copilot;

namespace StayFlow.Api.Services;

public interface ICopilotService
{
    Task<ApiResponse<ConversationCopilotSummaryResponse>> GetSummaryAsync(
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<ApiResponse<ConversationCopilotSuggestionsResponse>> GetSuggestedRepliesAsync(
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<ApiResponse<CopilotSuggestReplyResponse>> SuggestHostReplyAsync(
        Guid conversationId,
        CopilotSuggestReplyRequest request,
        CancellationToken cancellationToken);
}