using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class ChatService(
    IChatRepository chatRepository,
    IAIOrchestrator aiOrchestrator,
    ICurrentTenantContext currentTenantContext) : IChatService
{
    private const int MaxMessageLength = 2000;

    public async Task<ApiResponse<ChatMessageResponseDto>> SendMessageAsync(SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ChatMessageResponseDto>.Fail(tenantError, [tenantError]);
        }

        var validationErrors = ValidateMessageRequest(request);
        if (validationErrors.Count > 0)
        {
            return ApiResponse<ChatMessageResponseDto>.Fail("Chat message validation failed.", validationErrors);
        }

        var conversation = await ResolveConversationAsync(companyId, request, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ChatMessageResponseDto>.Fail("Conversation could not be verified.");
        }

        var guestMessage = NewMessage(companyId, conversation.Id, "Guest", request.Message.Trim());
        await chatRepository.AddMessageAsync(guestMessage, cancellationToken);
        await chatRepository.SaveChangesAsync(cancellationToken);

        var orchestrationResult = await aiOrchestrator.ProcessAsync(new AIOrchestrationRequest
        {
            GuestMessage = request.Message.Trim(),
            GuestId = conversation.GuestId,
            ConversationId = conversation.Id,
            Channel = request.Channel ?? conversation.Channel,
            ChannelIdentity = request.ChannelIdentity,
            ExplicitReservationReference = request.ExplicitReservationReference,
            ExplicitPropertyName = request.ExplicitPropertyName,
            CurrentTimestamp = request.CurrentTimestamp
        }, cancellationToken);

        var assistantMessage = NewMessage(
            companyId,
            conversation.Id,
            "Assistant",
            orchestrationResult.GuestSafeMessage,
            orchestrationResult.Outcome.ToString(),
            orchestrationResult.EscalationReason);

        if (orchestrationResult.Outcome is AIOrchestrationOutcome.EscalationRequired or AIOrchestrationOutcome.ProviderUnavailable)
        {
            conversation.Status = "Escalated";
        }

        await chatRepository.AddMessageAsync(assistantMessage, cancellationToken);
        await chatRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<ChatMessageResponseDto>.Ok(new ChatMessageResponseDto
        {
            ConversationId = conversation.Id,
            GuestMessage = MapMessage(guestMessage),
            AssistantMessage = MapMessage(assistantMessage),
            Outcome = orchestrationResult.Outcome,
            CandidateLabels = orchestrationResult.CandidateLabels,
            QuestionCategories = orchestrationResult.QuestionCategories,
            ValidationViolations = orchestrationResult.ValidationViolations,
            ProviderMetadata = orchestrationResult.ProviderMetadata
        }, "Chat message processed successfully.");
    }

    public async Task<ApiResponse<PagedResult<ChatMessageDto>>> GetHistoryAsync(ChatHistoryQueryParameters query, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PagedResult<ChatMessageDto>>.Fail(tenantError, [tenantError]);
        }

        if (query.ConversationId == Guid.Empty)
        {
            return ApiResponse<PagedResult<ChatMessageDto>>.Fail("ConversationId is required.", ["ConversationId is required."]);
        }

        var conversation = await chatRepository.GetConversationAsync(companyId, query.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<PagedResult<ChatMessageDto>>.Fail("Conversation was not found.");
        }

        var messages = await chatRepository.GetMessagesAsync(companyId, query, cancellationToken);
        return ApiResponse<PagedResult<ChatMessageDto>>.Ok(new PagedResult<ChatMessageDto>
        {
            Items = messages.Items.Select(MapMessage).ToList(),
            PageNumber = messages.PageNumber,
            PageSize = messages.PageSize,
            TotalCount = messages.TotalCount
        });
    }

    public async Task<ApiResponse<ChatStatusDto>> EscalateAsync(EscalateChatRequest request, CancellationToken cancellationToken)
    {
        return await UpdateStatusAsync(request.ConversationId, "Escalated", request.Reason, cancellationToken);
    }

    public async Task<ApiResponse<ChatStatusDto>> EndAsync(EndChatRequest request, CancellationToken cancellationToken)
    {
        return await UpdateStatusAsync(request.ConversationId, "Ended", null, cancellationToken);
    }

    private async Task<ApiResponse<ChatStatusDto>> UpdateStatusAsync(Guid conversationId, string status, string? reason, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ChatStatusDto>.Fail(tenantError, [tenantError]);
        }

        if (conversationId == Guid.Empty)
        {
            return ApiResponse<ChatStatusDto>.Fail("ConversationId is required.", ["ConversationId is required."]);
        }

        var conversation = await chatRepository.GetConversationAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ChatStatusDto>.Fail("Conversation was not found.");
        }

        conversation.Status = status;
        var internalBody = status == "Escalated"
            ? $"Conversation escalated{(string.IsNullOrWhiteSpace(reason) ? "." : $": {reason.Trim()}")}"
            : "Conversation ended.";
        await chatRepository.AddMessageAsync(NewMessage(companyId, conversation.Id, "System", internalBody, isInternal: true), cancellationToken);
        await chatRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<ChatStatusDto>.Ok(new ChatStatusDto
        {
            ConversationId = conversation.Id,
            Status = conversation.Status
        }, status == "Escalated" ? "Chat escalated successfully." : "Chat ended successfully.");
    }

    private async Task<Conversation?> ResolveConversationAsync(Guid companyId, SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        if (request.ConversationId is { } conversationId && conversationId != Guid.Empty)
        {
            return await chatRepository.GetConversationAsync(companyId, conversationId, cancellationToken);
        }

        if (request.GuestId is not { } guestId || request.PropertyId is not { } propertyId)
        {
            return null;
        }

        var guestBelongsToCompany = await chatRepository.GuestBelongsToCompanyAsync(companyId, guestId, cancellationToken);
        var propertyBelongsToCompany = await chatRepository.PropertyBelongsToCompanyAsync(companyId, propertyId, cancellationToken);
        if (!guestBelongsToCompany || !propertyBelongsToCompany)
        {
            return null;
        }

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            GuestId = guestId,
            PropertyId = propertyId,
            Channel = string.IsNullOrWhiteSpace(request.Channel) ? "Web" : request.Channel.Trim(),
            ExternalThreadId = null,
            Status = "Open"
        };

        await chatRepository.AddConversationAsync(conversation, cancellationToken);
        await chatRepository.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    private static IReadOnlyCollection<string> ValidateMessageRequest(SendChatMessageRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            errors.Add("Message is required.");
        }
        else if (request.Message.Trim().Length > MaxMessageLength)
        {
            errors.Add($"Message must be {MaxMessageLength} characters or fewer.");
        }

        if ((request.ConversationId is null || request.ConversationId == Guid.Empty)
            && (request.GuestId is null || request.GuestId == Guid.Empty || request.PropertyId is null || request.PropertyId == Guid.Empty))
        {
            errors.Add("GuestId and PropertyId are required when ConversationId is not supplied.");
        }

        return errors;
    }

    private bool TryGetCompanyId(out Guid companyId, out string error)
    {
        if (!currentTenantContext.IsAuthenticated)
        {
            companyId = Guid.Empty;
            error = "Authenticated tenant context is required.";
            return false;
        }

        if (currentTenantContext.CompanyId is not { } tenantCompanyId || tenantCompanyId == Guid.Empty)
        {
            companyId = Guid.Empty;
            error = "Authenticated tenant context is missing or invalid.";
            return false;
        }

        companyId = tenantCompanyId;
        error = string.Empty;
        return true;
    }

    private static ConversationMessage NewMessage(
        Guid companyId,
        Guid conversationId,
        string senderType,
        string body,
        string? aiOutcome = null,
        string? escalationReason = null,
        bool isInternal = false)
    {
        return new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConversationId = conversationId,
            SenderType = senderType,
            Body = body,
            AIOutcome = aiOutcome,
            EscalationReason = escalationReason,
            IsInternal = isInternal
        };
    }

    private static ChatMessageDto MapMessage(ConversationMessage message)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderType = message.SenderType,
            Body = message.Body,
            AIOutcome = message.AIOutcome,
            EscalationReason = message.EscalationReason,
            IsInternal = message.IsInternal,
            CreatedAt = message.CreatedAt
        };
    }
}
