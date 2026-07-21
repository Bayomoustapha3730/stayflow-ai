using System.Text.Json;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class ChatService(
    IConversationRepository conversationRepository,
    IConversationService conversationService,
    IAIOrchestrator aiOrchestrator,
    ICurrentTenantContext currentTenantContext,
    IOptions<ConversationOptions> options) : IChatService
{
    private const string HostWillRespondMessage = "Thanks, your message has been received. A host or support team member will respond shortly.";

    public async Task<ApiResponse<ChatMessageResponse>> SendGuestMessageAsync(SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ChatMessageResponse>.Fail(tenantError, [tenantError]);
        }

        var validationErrors = ValidateSendRequest(request);
        if (validationErrors.Count > 0)
        {
            return ApiResponse<ChatMessageResponse>.Fail("Chat message validation failed.", validationErrors);
        }

        var guest = await conversationRepository.GetGuestAsync(companyId, request.GuestId, cancellationToken);
        if (guest is null)
        {
            return ApiResponse<ChatMessageResponse>.Fail("Guest was not found.");
        }

        var identityValidation = ValidateChannelIdentity(request.Channel, request.ChannelIdentity, guest);
        if (!identityValidation.Success)
        {
            return ApiResponse<ChatMessageResponse>.Fail(identityValidation.Message, [identityValidation.Message]);
        }

        if (!string.IsNullOrWhiteSpace(request.ExternalMessageId))
        {
            var duplicate = await conversationRepository.FindByExternalMessageIdAsync(companyId, request.ExternalMessageId.Trim(), cancellationToken);
            if (duplicate is not null)
            {
                await AuditAsync(companyId, duplicate.ConversationId, request.GuestId, "DuplicateExternalMessageIgnored", request.Channel, null, null, cancellationToken);
                var duplicateConversation = await conversationRepository.GetByIdForCompanyAsync(companyId, duplicate.ConversationId, cancellationToken);
                return ApiResponse<ChatMessageResponse>.Ok(new ChatMessageResponse
                {
                    ConversationId = duplicate.ConversationId,
                    ConversationStatus = duplicateConversation?.Status ?? ConversationStatus.Open,
                    GuestMessage = MapMessage(duplicate),
                    HumanTakeoverEnabled = duplicateConversation?.HumanTakeoverEnabled == true,
                    RequiresHostAttention = RequiresHostAttention(duplicateConversation?.Status ?? ConversationStatus.Open, duplicateConversation?.HumanTakeoverEnabled == true),
                    EscalationReason = duplicateConversation?.EscalationReason,
                    CreatedAt = duplicate.SentAt
                }, "Message already exists.");
            }
        }

        var conversationResponse = request.ConversationId is { } conversationId && conversationId != Guid.Empty
            ? await ValidateExistingConversationAsync(companyId, conversationId, request, cancellationToken)
            : await conversationService.CreateOrGetConversationAsync(new CreateConversationRequest
            {
                GuestId = request.GuestId,
                ReservationId = request.ReservationId,
                PropertyId = request.PropertyId,
                Channel = request.Channel,
                ChannelIdentity = NormalizeIdentity(request.ChannelIdentity)
            }, cancellationToken);

        if (!conversationResponse.Success || conversationResponse.Data is null)
        {
            return ApiResponse<ChatMessageResponse>.Fail(conversationResponse.Message, conversationResponse.Errors);
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationResponse.Data.Id, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ChatMessageResponse>.Fail("Conversation was not found.");
        }

        if (conversation.Status == ConversationStatus.Closed)
        {
            return ApiResponse<ChatMessageResponse>.Fail("Conversation state does not allow this message.");
        }

        var sentAt = (request.CurrentTimestamp ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var guestMessage = await conversationService.AddGuestMessageAsync(conversation.Id, new AddGuestMessageRequest
        {
            Content = request.Message,
            ExternalMessageId = request.ExternalMessageId,
            SentAt = sentAt
        }, cancellationToken);
        if (!guestMessage.Success || guestMessage.Data is null)
        {
            return ApiResponse<ChatMessageResponse>.Fail(guestMessage.Message, guestMessage.Errors);
        }

        await AuditAsync(companyId, conversation.Id, request.GuestId, "ChatGuestMessageReceived", request.Channel, null, null, cancellationToken);

        conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversation.Id, cancellationToken) ?? conversation;
        if (conversation.HumanTakeoverEnabled || conversation.Status is ConversationStatus.HumanManaged or ConversationStatus.AwaitingHost or ConversationStatus.Escalated)
        {
            conversation.Status = conversation.Status == ConversationStatus.HumanManaged
                ? ConversationStatus.HumanManaged
                : ConversationStatus.AwaitingHost;
            conversation.HumanTakeoverEnabled = true;
            var assistant = await conversationService.AddHostMessageAsync(conversation.Id, new AddHostMessageRequest
            {
                Content = HostWillRespondMessage,
                SentAt = DateTimeOffset.UtcNow
            }, cancellationToken);
            await AuditAsync(companyId, conversation.Id, request.GuestId, "HumanManagedMessageReceived", request.Channel, AIOrchestrationOutcome.EscalationRequired, null, cancellationToken);
            return ApiResponse<ChatMessageResponse>.Ok(ToChatMessageResponse(conversation, guestMessage.Data, assistant.Data, null, [], []), "Chat message received.");
        }

        var orchestration = await aiOrchestrator.ProcessAsync(new AIOrchestrationRequest
        {
            GuestMessage = request.Message.Trim(),
            GuestId = request.GuestId,
            ConversationId = conversation.Id,
            Channel = request.Channel.ToString(),
            ChannelIdentity = request.ChannelIdentity,
            ExplicitReservationReference = request.ExplicitReservationReference,
            ExplicitPropertyName = request.ExplicitPropertyName,
            CurrentTimestamp = sentAt
        }, cancellationToken);

        UpdateConversationStatusFromAI(conversation, orchestration);
        var aiMessage = await conversationService.AddAIMessageAsync(conversation.Id, orchestration.GuestSafeMessage, orchestration, cancellationToken);
        await AuditAsync(companyId, conversation.Id, request.GuestId, orchestration.Outcome == AIOrchestrationOutcome.Responded ? "ChatAIResponseStored" : "ChatEscalated", request.Channel, orchestration.Outcome, orchestration.ProviderMetadata?.ProviderName, cancellationToken);

        return ApiResponse<ChatMessageResponse>.Ok(
            ToChatMessageResponse(conversation, guestMessage.Data, aiMessage.Data, orchestration.ProviderMetadata, orchestration.QuestionCategories, orchestration.CandidateLabels),
            "Chat message processed successfully.");
    }

    public async Task<ApiResponse<ChatConversationResponse>> GetGuestConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ChatConversationResponse>.Fail(tenantError, [tenantError]);
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ChatConversationResponse>.Fail("Conversation was not found.");
        }

        var history = await GetVisibleMessagesAsync(companyId, conversation.Id, new ChatHistoryQueryParameters { PageSize = 10 }, cancellationToken);
        return ApiResponse<ChatConversationResponse>.Ok(MapConversation(conversation, history.Items));
    }

    public async Task<ApiResponse<ChatHistoryResponse>> GetGuestHistoryAsync(Guid conversationId, ChatHistoryQueryParameters query, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ChatHistoryResponse>.Fail(tenantError, [tenantError]);
        }

        if (await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken) is null)
        {
            return ApiResponse<ChatHistoryResponse>.Fail("Conversation was not found.");
        }

        var messages = await GetVisibleMessagesAsync(companyId, conversationId, query, cancellationToken);
        return ApiResponse<ChatHistoryResponse>.Ok(new ChatHistoryResponse
        {
            ConversationId = conversationId,
            Messages = messages
        });
    }

    public async Task<ApiResponse<ChatStatusResponse>> EscalateGuestConversationAsync(Guid conversationId, EscalateChatRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateConversationGuestAsync(conversationId, request.GuestId, cancellationToken);
        if (!validation.Success || validation.Data is null)
        {
            return ApiResponse<ChatStatusResponse>.Fail(validation.Message, validation.Errors);
        }

        var escalation = await conversationService.EscalateConversationAsync(conversationId, new EscalateConversationRequest
        {
            Reason = Truncate(request.Reason, 120)
        }, cancellationToken);
        if (!escalation.Success || escalation.Data is null)
        {
            return ApiResponse<ChatStatusResponse>.Fail(escalation.Message, escalation.Errors);
        }

        await conversationService.AddAIMessageAsync(conversationId, HostWillRespondMessage, new AIOrchestrationResult
        {
            Outcome = AIOrchestrationOutcome.EscalationRequired,
            GuestSafeMessage = HostWillRespondMessage,
            EscalationReason = "GuestRequestedEscalation"
        }, cancellationToken);
        await AuditAsync(validation.Data.CompanyId, conversationId, request.GuestId, "ChatEscalated", validation.Data.Channel, AIOrchestrationOutcome.EscalationRequired, null, cancellationToken);

        return ApiResponse<ChatStatusResponse>.Ok(MapStatus(escalation.Data, HostWillRespondMessage), "Chat escalated successfully.");
    }

    public async Task<ApiResponse<ChatStatusResponse>> EndGuestConversationAsync(Guid conversationId, EndChatRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateConversationGuestAsync(conversationId, request.GuestId, cancellationToken);
        if (!validation.Success || validation.Data is null)
        {
            return ApiResponse<ChatStatusResponse>.Fail(validation.Message, validation.Errors);
        }

        var message = "This conversation has been closed.";
        await conversationService.AddAIMessageAsync(conversationId, message, new AIOrchestrationResult
        {
            Outcome = AIOrchestrationOutcome.Responded,
            GuestSafeMessage = message
        }, cancellationToken);

        var closed = await conversationService.CloseConversationAsync(conversationId, cancellationToken);
        if (!closed.Success || closed.Data is null)
        {
            return ApiResponse<ChatStatusResponse>.Fail(closed.Message, closed.Errors);
        }

        await AuditAsync(validation.Data.CompanyId, conversationId, request.GuestId, "ChatEnded", validation.Data.Channel, null, null, cancellationToken);
        return ApiResponse<ChatStatusResponse>.Ok(MapStatus(closed.Data, message), "Chat ended successfully.");
    }

    private async Task<ApiResponse<ConversationDetailResponse>> ValidateExistingConversationAsync(Guid companyId, Guid conversationId, SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Conversation was not found.");
        }

        if (conversation.GuestId != request.GuestId)
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Conversation guest identity conflicts with the supplied guest identity.");
        }

        if (conversation.Channel != request.Channel)
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Conversation channel conflicts with the supplied channel.");
        }

        if (!IsIdentityCompatible(request.Channel, request.ChannelIdentity, conversation.ChannelIdentity))
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Conversation channel identity conflicts with the supplied channel identity.");
        }

        if (conversation.Status == ConversationStatus.Closed)
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Conversation state does not allow this message.");
        }

        return ApiResponse<ConversationDetailResponse>.Ok(ToConversationDetail(conversation));
    }

    private async Task<ApiResponse<Conversation>> ValidateConversationGuestAsync(Guid conversationId, Guid guestId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<Conversation>.Fail(tenantError, [tenantError]);
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<Conversation>.Fail("Conversation was not found.");
        }

        if (conversation.GuestId != guestId)
        {
            return ApiResponse<Conversation>.Fail("Conversation guest identity conflicts with the supplied guest identity.");
        }

        return ApiResponse<Conversation>.Ok(conversation);
    }

    private async Task<PagedResult<ChatVisibleMessageDto>> GetVisibleMessagesAsync(Guid companyId, Guid conversationId, ChatHistoryQueryParameters query, CancellationToken cancellationToken)
    {
        var cappedQuery = new ConversationHistoryQueryParameters
        {
            IncludeInternal = false,
            PageNumber = query.PageNumber,
            PageSize = Math.Min(query.NormalizedPageSize, options.Value.MaxHistoryMessages)
        };
        var messages = await conversationRepository.GetMessagesAsync(companyId, conversationId, cappedQuery, cancellationToken);
        return new PagedResult<ChatVisibleMessageDto>
        {
            Items = messages.Items.Where(message => !message.IsInternal).Select(MapMessage).ToList(),
            PageNumber = messages.PageNumber,
            PageSize = messages.PageSize,
            TotalCount = messages.TotalCount
        };
    }

    private void UpdateConversationStatusFromAI(Conversation conversation, AIOrchestrationResult result)
    {
        switch (result.Outcome)
        {
            case AIOrchestrationOutcome.Responded:
                conversation.Status = ConversationStatus.Open;
                break;
            case AIOrchestrationOutcome.ClarificationRequired:
                conversation.Status = ConversationStatus.AwaitingGuest;
                break;
            case AIOrchestrationOutcome.EscalationRequired:
            case AIOrchestrationOutcome.ProviderUnavailable:
                conversation.Status = ConversationStatus.AwaitingHost;
                conversation.HumanTakeoverEnabled = true;
                conversation.EscalationReason = result.EscalationReason ?? result.Outcome.ToString();
                break;
            case AIOrchestrationOutcome.NoEligibleReservation:
            case AIOrchestrationOutcome.Blocked:
                conversation.Status = ConversationStatus.AwaitingHost;
                break;
        }
    }

    private ChatMessageResponse ToChatMessageResponse(
        Conversation conversation,
        ConversationMessageResponse guestMessage,
        ConversationMessageResponse? assistantMessage,
        AIProviderMetadata? providerMetadata,
        IReadOnlyCollection<QuestionContextCategory> categories,
        IReadOnlyCollection<ReservationCandidateLabel> candidateLabels)
    {
        return new ChatMessageResponse
        {
            ConversationId = conversation.Id,
            ConversationStatus = conversation.Status,
            GuestMessage = MapMessage(guestMessage),
            AssistantMessage = assistantMessage is null ? null : MapMessage(assistantMessage),
            HumanTakeoverEnabled = conversation.HumanTakeoverEnabled,
            RequiresHostAttention = RequiresHostAttention(conversation.Status, conversation.HumanTakeoverEnabled),
            EscalationReason = conversation.EscalationReason,
            ProviderMetadata = providerMetadata is null
                ? null
                : new ChatProviderMetadataResponse
                {
                    ProviderName = providerMetadata.ProviderName,
                    ModelName = providerMetadata.ModelName,
                    RequestId = providerMetadata.RequestId
                },
            QuestionCategories = categories,
            CandidateLabels = candidateLabels,
            CreatedAt = guestMessage.SentAt
        };
    }

    private static ChatConversationResponse MapConversation(Conversation conversation, IReadOnlyCollection<ChatVisibleMessageDto> messages)
    {
        return new ChatConversationResponse
        {
            ConversationId = conversation.Id,
            Status = conversation.Status,
            Channel = conversation.Channel,
            Subject = conversation.Subject,
            HumanTakeoverEnabled = conversation.HumanTakeoverEnabled,
            RequiresHostAttention = RequiresHostAttention(conversation.Status, conversation.HumanTakeoverEnabled),
            StartedAt = conversation.StartedAt,
            LastActivityAt = conversation.LastActivityAt,
            ClosedAt = conversation.ClosedAt,
            Reservation = conversation.Reservation is null && conversation.Property is null
                ? null
                : new ChatReservationSummary
                {
                    ConfirmationNumber = conversation.Reservation?.ConfirmationNumber,
                    CheckInDate = conversation.Reservation?.CheckInDate,
                    CheckOutDate = conversation.Reservation?.CheckOutDate,
                    PropertyDisplayName = conversation.Property?.Name
                },
            RecentMessages = messages
        };
    }

    private static ChatStatusResponse MapStatus(ConversationDetailResponse conversation, string guestSafeMessage)
    {
        return new ChatStatusResponse
        {
            ConversationId = conversation.Id,
            Status = conversation.Status,
            HumanTakeoverEnabled = conversation.HumanTakeoverEnabled,
            RequiresHostAttention = RequiresHostAttention(conversation.Status, conversation.HumanTakeoverEnabled),
            GuestSafeMessage = guestSafeMessage
        };
    }

    private static ConversationDetailResponse ToConversationDetail(Conversation conversation)
    {
        return new ConversationDetailResponse
        {
            Id = conversation.Id,
            GuestId = conversation.GuestId,
            ReservationId = conversation.ReservationId,
            PropertyId = conversation.PropertyId,
            Channel = conversation.Channel.ToString(),
            ChannelIdentity = conversation.ChannelIdentity,
            Status = conversation.Status,
            Subject = conversation.Subject,
            HumanTakeoverEnabled = conversation.HumanTakeoverEnabled,
            EscalationReason = conversation.EscalationReason,
            StartedAt = conversation.StartedAt,
            LastActivityAt = conversation.LastActivityAt,
            ClosedAt = conversation.ClosedAt,
            Guest = new ConversationGuestSummary { Id = conversation.GuestId, GuestId = conversation.GuestId }
        };
    }

    private IReadOnlyCollection<string> ValidateSendRequest(SendChatMessageRequest request)
    {
        var errors = new List<string>();
        if (request.GuestId == Guid.Empty)
        {
            errors.Add("GuestId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            errors.Add("Message is required.");
        }
        else if (request.Message.Trim().Length > options.Value.MaxMessageCharacters)
        {
            errors.Add($"Message must be {options.Value.MaxMessageCharacters} characters or fewer.");
        }

        return errors;
    }

    private static ChannelIdentityValidationResult ValidateChannelIdentity(GuestChannel channel, string? channelIdentity, Guest guest)
    {
        return channel switch
        {
            GuestChannel.WhatsApp or GuestChannel.SMS => ValidatePhoneIdentity(channelIdentity, guest.PhoneNumber),
            GuestChannel.Email => ValidateEmailIdentity(channelIdentity, guest.Email),
            GuestChannel.Web => string.IsNullOrWhiteSpace(channelIdentity)
                ? ChannelIdentityValidationResult.Ok()
                : ChannelIdentityValidationResult.Ok(),
            _ => ChannelIdentityValidationResult.Fail("Unsupported channel.")
        };
    }

    private static ChannelIdentityValidationResult ValidatePhoneIdentity(string? channelIdentity, string? guestPhone)
    {
        if (string.IsNullOrWhiteSpace(channelIdentity) || string.IsNullOrWhiteSpace(guestPhone))
        {
            return ChannelIdentityValidationResult.Fail("Channel identity is required.");
        }

        return NormalizePhone(channelIdentity) == NormalizePhone(guestPhone)
            ? ChannelIdentityValidationResult.Ok()
            : ChannelIdentityValidationResult.Fail("Channel identity conflicts with the resolved guest identity.");
    }

    private static ChannelIdentityValidationResult ValidateEmailIdentity(string? channelIdentity, string? guestEmail)
    {
        if (string.IsNullOrWhiteSpace(channelIdentity) || string.IsNullOrWhiteSpace(guestEmail))
        {
            return ChannelIdentityValidationResult.Fail("Channel identity is required.");
        }

        return string.Equals(NormalizeEmail(channelIdentity), NormalizeEmail(guestEmail), StringComparison.Ordinal)
            ? ChannelIdentityValidationResult.Ok()
            : ChannelIdentityValidationResult.Fail("Channel identity conflicts with the resolved guest identity.");
    }

    private static bool IsIdentityCompatible(GuestChannel channel, string? requestIdentity, string? conversationIdentity)
    {
        if (channel == GuestChannel.Web && string.IsNullOrWhiteSpace(requestIdentity))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(requestIdentity) && string.IsNullOrWhiteSpace(conversationIdentity))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(requestIdentity) || string.IsNullOrWhiteSpace(conversationIdentity))
        {
            return false;
        }

        return channel switch
        {
            GuestChannel.WhatsApp or GuestChannel.SMS => NormalizePhone(requestIdentity) == NormalizePhone(conversationIdentity),
            GuestChannel.Email => NormalizeEmail(requestIdentity) == NormalizeEmail(conversationIdentity),
            _ => string.Equals(requestIdentity.Trim(), conversationIdentity.Trim(), StringComparison.Ordinal)
        };
    }

    private static bool RequiresHostAttention(ConversationStatus status, bool humanTakeoverEnabled)
    {
        return humanTakeoverEnabled || status is ConversationStatus.AwaitingHost or ConversationStatus.Escalated or ConversationStatus.HumanManaged;
    }

    private static ChatVisibleMessageDto MapMessage(ConversationMessageResponse message)
    {
        return new ChatVisibleMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderType = message.SenderType,
            Content = message.Content,
            MessageType = message.MessageType,
            SentAt = message.SentAt
        };
    }

    private static ChatVisibleMessageDto MapMessage(ConversationMessage message)
    {
        return new ChatVisibleMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderType = message.SenderType,
            Content = message.Content,
            MessageType = message.MessageType,
            SentAt = message.SentAt
        };
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

    private async Task AuditAsync(Guid companyId, Guid conversationId, Guid guestId, string action, GuestChannel channel, AIOrchestrationOutcome? outcome, string? providerName, CancellationToken cancellationToken)
    {
        await conversationRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "Chat",
            EntityId = conversationId,
            Action = action,
            Details = JsonSerializer.Serialize(new
            {
                currentTenantContext.CorrelationId,
                ConversationId = conversationId,
                GuestId = guestId,
                Channel = channel.ToString(),
                OrchestrationOutcome = outcome?.ToString(),
                ProviderName = providerName,
                Timestamp = DateTimeOffset.UtcNow
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeIdentity(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizePhone(string value) => new(value.Where(char.IsDigit).ToArray());
    private static string NormalizeEmail(string value) => value.Trim().ToUpperInvariant();
    private static string? Truncate(string? value, int maxLength) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];

    private readonly record struct ChannelIdentityValidationResult(bool Success, string Message)
    {
        public static ChannelIdentityValidationResult Ok() => new(true, string.Empty);
        public static ChannelIdentityValidationResult Fail(string message) => new(false, message);
    }
}
