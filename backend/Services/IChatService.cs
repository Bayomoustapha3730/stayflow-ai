using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.ConciergeActions;

namespace StayFlow.Api.Services;

public interface IChatService
{
    // whatsAppIntegrationId is for trusted internal callers only (e.g. the WhatsApp webhook
    // processor) that already resolved the integration by PhoneNumberId; never bind from
    // untrusted request input.
    Task<ApiResponse<ChatMessageResponse>> SendGuestMessageAsync(SendChatMessageRequest request, CancellationToken cancellationToken, Guid? whatsAppIntegrationId = null);
    Task<ApiResponse<ChatConversationResponse>> GetGuestConversationAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<ApiResponse<ChatHistoryResponse>> GetGuestHistoryAsync(Guid conversationId, ChatHistoryQueryParameters query, CancellationToken cancellationToken);
    Task<ApiResponse<ChatStatusResponse>> EscalateGuestConversationAsync(Guid conversationId, EscalateChatRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ChatStatusResponse>> EndGuestConversationAsync(Guid conversationId, EndChatRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ChatMessageResponse>> ConfirmPendingActionAsync(Guid conversationId, Guid actionId, ConfirmPendingActionRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ChatMessageResponse>> CancelPendingActionAsync(Guid conversationId, Guid actionId, CancelPendingActionRequest request, CancellationToken cancellationToken);
}
