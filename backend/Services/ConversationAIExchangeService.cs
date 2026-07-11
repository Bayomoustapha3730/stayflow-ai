using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class ConversationAIExchangeService(
    IConversationRepository conversationRepository,
    IConversationService conversationService,
    IAIOrchestrator aiOrchestrator,
    ICurrentTenantContext currentTenantContext) : IConversationAIExchangeService
{
    public async Task<ApiResponse<AIOrchestrationResult>> ProcessGuestMessageAsync(Guid conversationId, AddGuestMessageRequest request, CancellationToken cancellationToken)
    {
        if (currentTenantContext.CompanyId is not { } companyId || companyId == Guid.Empty || !currentTenantContext.IsAuthenticated)
        {
            return ApiResponse<AIOrchestrationResult>.Fail("Authenticated tenant context is required.");
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<AIOrchestrationResult>.Fail("Conversation was not found.");
        }

        if (conversation.Status == ConversationStatus.HumanManaged || conversation.HumanTakeoverEnabled)
        {
            return ApiResponse<AIOrchestrationResult>.Fail("Conversation is in human takeover mode.");
        }

        var storedGuestMessage = await conversationService.AddGuestMessageAsync(conversationId, request, cancellationToken);
        if (!storedGuestMessage.Success)
        {
            return ApiResponse<AIOrchestrationResult>.Fail(storedGuestMessage.Message, storedGuestMessage.Errors);
        }

        var result = await aiOrchestrator.ProcessAsync(new AIOrchestrationRequest
        {
            GuestMessage = request.Content,
            GuestId = conversation.GuestId,
            ConversationId = conversation.Id,
            Channel = conversation.Channel.ToString(),
            ChannelIdentity = conversation.ChannelIdentity,
            CurrentTimestamp = request.SentAt ?? DateTimeOffset.UtcNow
        }, cancellationToken);

        await conversationService.AddAIMessageAsync(conversationId, result.GuestSafeMessage, result, cancellationToken);
        return ApiResponse<AIOrchestrationResult>.Ok(result, "Conversation AI exchange processed.");
    }
}
