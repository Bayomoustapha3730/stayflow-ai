using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;

namespace StayFlow.Api.Services;

public interface IConversationService
{
    Task<ApiResponse<ConversationDetailResponse>> CreateOrGetConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationDetailResponse>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationHistoryResponse>> GetConversationHistoryAsync(Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationMessageResponse>> AddGuestMessageAsync(Guid conversationId, AddGuestMessageRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationMessageResponse>> AddAIMessageAsync(Guid conversationId, string content, DTOs.AIOrchestration.AIOrchestrationResult result, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationMessageResponse>> AddHostMessageAsync(Guid conversationId, AddHostMessageRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationMessageResponse>> AddInternalNoteAsync(Guid conversationId, AddInternalNoteRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationDetailResponse>> EscalateConversationAsync(Guid conversationId, EscalateConversationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationDetailResponse>> EnableHumanTakeoverAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationDetailResponse>> ReturnToAIModeAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationDetailResponse>> ResolveConversationAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationDetailResponse>> CloseConversationAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<ApiResponse<ConversationListResponse>> GetConversationsAsync(
        ConversationListQueryParameters query,
        CancellationToken cancellationToken);
}
