using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Chat;

namespace StayFlow.Api.Services;

public interface IChatService
{
    Task<ApiResponse<ChatMessageResponseDto>> SendMessageAsync(SendChatMessageRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<PagedResult<ChatMessageDto>>> GetHistoryAsync(ChatHistoryQueryParameters query, CancellationToken cancellationToken);
    Task<ApiResponse<ChatStatusDto>> EscalateAsync(EscalateChatRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ChatStatusDto>> EndAsync(EndChatRequest request, CancellationToken cancellationToken);
}
