using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Conversations;

namespace StayFlow.Api.Services;

public interface IConversationAIExchangeService
{
    Task<ApiResponse<AIOrchestrationResult>> ProcessGuestMessageAsync(Guid conversationId, AddGuestMessageRequest request, CancellationToken cancellationToken);
}
