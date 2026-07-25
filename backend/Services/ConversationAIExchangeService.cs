using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Services;

public sealed class ConversationAIExchangeService(
    IConversationRepository conversationRepository,
    IConversationService conversationService,
    IAIReplyOrchestrator aiReplyOrchestrator,
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

        var replyResult = await aiReplyOrchestrator.OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
        {
            ConversationId = conversation.Id,
            Operation = AIReplyOperation.GeneratedHostReply,
            CorrelationId = currentTenantContext.CorrelationId
        }, cancellationToken);

        if (replyResult is null)
        {
            return ApiResponse<AIOrchestrationResult>.Fail("Conversation was not found.");
        }

        var result = MapGuestOrchestrationResult(replyResult);

        await conversationService.AddAIMessageAsync(conversationId, result.GuestSafeMessage, result, cancellationToken);
        return ApiResponse<AIOrchestrationResult>.Ok(result, "Conversation AI exchange processed.");
    }

    private static AIOrchestrationResult MapGuestOrchestrationResult(AIReplyOrchestrationResult replyResult)
    {
        var requiresReview = replyResult.RequiresHumanReview;

        return new AIOrchestrationResult
        {
            Outcome = requiresReview ? AIOrchestrationOutcome.EscalationRequired : AIOrchestrationOutcome.Responded,
            GuestSafeMessage = requiresReview
                ? AIOrchestrationSafeMessages.HostAssistanceRequired
                : (string.IsNullOrWhiteSpace(replyResult.Output) ? AIOrchestrationSafeMessages.GeneralResponseUnavailable : replyResult.Output),
            QuestionCategories = MapQuestionCategories(replyResult.DetectedIntent?.Intent),
            ProviderMetadata = new AIProviderMetadata
            {
                ProviderName = replyResult.Provider,
                ModelName = replyResult.IsMock ? "deterministic" : null,
                RequestId = null,
                DurationMs = replyResult.DurationMilliseconds
            },
            EscalationReason = requiresReview ? "RequiresHumanReview" : null
        };
    }

    private static IReadOnlyCollection<QuestionContextCategory> MapQuestionCategories(GuestIntent? intent)
    {
        if (intent is null)
        {
            return [QuestionContextCategory.General];
        }

        return intent.Value switch
        {
            GuestIntent.WiFi => [QuestionContextCategory.WiFi],
            GuestIntent.CheckIn or GuestIntent.EarlyCheckIn or GuestIntent.LateArrival => [QuestionContextCategory.CheckIn],
            GuestIntent.Checkout => [QuestionContextCategory.CheckOut],
            GuestIntent.Parking => [QuestionContextCategory.Parking],
            GuestIntent.HouseRules or GuestIntent.Noise => [QuestionContextCategory.HouseRules],
            GuestIntent.Amenities => [QuestionContextCategory.Amenities],
            GuestIntent.Laundry => [QuestionContextCategory.Laundry],
            GuestIntent.Emergency or GuestIntent.Maintenance => [QuestionContextCategory.Emergency],
            GuestIntent.GeneralQuestion => [QuestionContextCategory.General],
            _ => [QuestionContextCategory.General]
        };
    }
}
