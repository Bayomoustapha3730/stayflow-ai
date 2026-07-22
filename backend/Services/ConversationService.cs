using System.Text.Json;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class ConversationService(
    IConversationRepository conversationRepository,
    ICurrentTenantContext currentTenantContext,
    IConversationStatusTransitionPolicy transitionPolicy,
    IConversationRealtimePublisher realtimePublisher,
    IOptions<ConversationOptions> options) : IConversationService
{
    public async Task<ApiResponse<ConversationListResponse>> GetConversationsAsync(ConversationListQueryParameters query, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ConversationListResponse>.Fail(tenantError, [tenantError]);
        }

        var validationErrors = ValidateListQuery(query);
        if (validationErrors.Count > 0)
        {
            return ApiResponse<ConversationListResponse>.Fail("Conversation list query validation failed.", validationErrors);
        }

        var normalizedQuery = new ConversationListQueryParameters
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Status = query.Status,
            PropertyId = query.PropertyId,
            RequiresHostAttention = query.RequiresHostAttention,
            Search = NormalizeIdentity(query.Search)
        };

        var result = await conversationRepository.ListConversationsAsync(companyId, normalizedQuery, cancellationToken);
        var hostUserId = currentTenantContext.UserId;
        var unreadByConversation = hostUserId is { } currentHostUserId && currentHostUserId != Guid.Empty
            ? await conversationRepository.GetUnreadMessageCountsForHostAsync(
                companyId,
                currentHostUserId,
                result.Items.Select(item => item.ConversationId).ToList(),
                cancellationToken)
            : new Dictionary<Guid, int>();
        var readStatesByConversation = hostUserId is { } hostReadStateUserId && hostReadStateUserId != Guid.Empty
            ? (await conversationRepository.GetReadStatesForParticipantAsync(companyId, ConversationParticipantKind.HostUser, hostReadStateUserId, cancellationToken))
                .ToDictionary(state => state.ConversationId, state => state)
            : new Dictionary<Guid, ConversationParticipantReadState>();

        var enrichedItems = result.Items.Select(item => EnrichSummaryWithReadData(
            item,
            unreadByConversation.TryGetValue(item.ConversationId, out var unread) ? unread : 0,
            readStatesByConversation.TryGetValue(item.ConversationId, out var readState) ? readState.LastReadAt : null)).ToList();

        var totalUnreadCount = hostUserId is { } unreadHostUserId && unreadHostUserId != Guid.Empty
            ? await conversationRepository.GetTotalUnreadCountForHostAsync(companyId, unreadHostUserId, cancellationToken)
            : 0;

        return ApiResponse<ConversationListResponse>.Ok(new ConversationListResponse
        {
            Items = enrichedItems,
            TotalCount = result.TotalCount,
            Page = result.PageNumber,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages,
            TotalUnreadCount = totalUnreadCount
        });
    }

    public async Task<ApiResponse<ConversationDetailResponse>> CreateOrGetConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ConversationDetailResponse>.Fail(tenantError, [tenantError]);
        }

        var validation = await ValidateConversationAssociationsAsync(companyId, request, cancellationToken);
        if (!validation.Success)
        {
            return ApiResponse<ConversationDetailResponse>.Fail(validation.Message, validation.Errors);
        }

        var now = DateTimeOffset.UtcNow;
        var normalizedIdentity = NormalizeIdentity(request.ChannelIdentity);
        var cutoff = now.AddMinutes(-options.Value.ReuseOpenConversationMinutes);
        var existing = await conversationRepository.GetOpenConversationAsync(companyId, request.GuestId, request.Channel, normalizedIdentity, cutoff, cancellationToken);
        if (existing is not null)
        {
            return ApiResponse<ConversationDetailResponse>.Ok(MapDetail(existing), "Conversation retrieved successfully.");
        }

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            GuestId = request.GuestId,
            ReservationId = request.ReservationId,
            PropertyId = request.ReservationId.HasValue ? validation.Data!.ReservationPropertyId : request.PropertyId,
            Channel = request.Channel,
            ChannelIdentity = normalizedIdentity,
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            AssignedUserId = request.AssignedUserId,
            Status = ConversationStatus.Open,
            StartedAt = now,
            LastActivityAt = now
        };

        await conversationRepository.AddConversationAsync(conversation, cancellationToken);
        await AuditAsync(companyId, conversation.Id, "ConversationCreated", conversation.Status, null, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        var hydrated = await conversationRepository.GetByIdForCompanyAsync(companyId, conversation.Id, cancellationToken) ?? conversation;
        return ApiResponse<ConversationDetailResponse>.Ok(MapDetail(hydrated), "Conversation created successfully.");
    }

    public async Task<ApiResponse<ConversationDetailResponse>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await GetConversationForTenantAsync(conversationId, cancellationToken);
        return conversation is null
            ? ApiResponse<ConversationDetailResponse>.Fail("Conversation was not found.")
            : ApiResponse<ConversationDetailResponse>.Ok(MapDetail(conversation));
    }

    public async Task<ApiResponse<ConversationHistoryResponse>> GetConversationHistoryAsync(Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ConversationHistoryResponse>.Fail(tenantError, [tenantError]);
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ConversationHistoryResponse>.Fail("Conversation was not found.");
        }

        var cappedQuery = new ConversationHistoryQueryParameters
        {
            IncludeInternal = query.IncludeInternal,
            PageNumber = query.PageNumber,
            PageSize = Math.Min(query.NormalizedPageSize, options.Value.MaxHistoryMessages)
        };
        var messages = await conversationRepository.GetMessagesAsync(companyId, conversationId, cappedQuery, cancellationToken);
        return ApiResponse<ConversationHistoryResponse>.Ok(new ConversationHistoryResponse
        {
            ConversationId = conversationId,
            Messages = new PagedResult<ConversationMessageResponse>
            {
                Items = messages.Items.Select(MapMessage).ToList(),
                PageNumber = messages.PageNumber,
                PageSize = messages.PageSize,
                TotalCount = messages.TotalCount
            }
        });
    }

    public async Task<ApiResponse<ConversationMessageResponse>> AddGuestMessageAsync(Guid conversationId, AddGuestMessageRequest request, CancellationToken cancellationToken)
    {
        var duplicate = await FindDuplicateAsync(request.ExternalMessageId, cancellationToken);
        if (duplicate is not null)
        {
            return ApiResponse<ConversationMessageResponse>.Ok(MapMessage(duplicate), "Message already exists.");
        }

        return await AddMessageAsync(conversationId, ConversationSenderType.Guest, ConversationMessageType.Text, request.Content, request.SentAt, request.ExternalMessageId, null, false, "GuestMessageStored", cancellationToken);
    }

    public async Task<ApiResponse<ConversationMessageResponse>> AddAIMessageAsync(Guid conversationId, string content, AIOrchestrationResult result, CancellationToken cancellationToken)
    {
        return await AddMessageAsync(
            conversationId,
            ConversationSenderType.AI,
            result.Outcome == AIOrchestrationOutcome.Responded ? ConversationMessageType.Text : ConversationMessageType.SystemEvent,
            content,
            DateTimeOffset.UtcNow,
            null,
            result,
            result.Outcome != AIOrchestrationOutcome.Responded,
            result.Outcome == AIOrchestrationOutcome.Responded ? "AIMessageStored" : "ConversationEscalated",
            cancellationToken);
    }

    public async Task<ApiResponse<ConversationMessageResponse>> AddHostMessageAsync(Guid conversationId, AddHostMessageRequest request, CancellationToken cancellationToken)
    {
        return await AddMessageAsync(conversationId, ConversationSenderType.Host, ConversationMessageType.Text, request.Content, request.SentAt, null, null, false, "HostMessageStored", cancellationToken);
    }

    public async Task<ApiResponse<ConversationMessageResponse>> AddInternalNoteAsync(Guid conversationId, AddInternalNoteRequest request, CancellationToken cancellationToken)
    {
        return await AddMessageAsync(conversationId, ConversationSenderType.System, ConversationMessageType.InternalNote, request.Content, DateTimeOffset.UtcNow, null, null, true, "InternalNoteAdded", cancellationToken);
    }

    public Task<ApiResponse<ConversationDetailResponse>> EscalateConversationAsync(Guid conversationId, EscalateConversationRequest request, CancellationToken cancellationToken)
    {
        return TransitionAsync(conversationId, ConversationStatus.Escalated, "ConversationEscalated", conversation =>
        {
            conversation.HumanTakeoverEnabled = true;
            conversation.EscalationReason = string.IsNullOrWhiteSpace(request.Reason) ? "ManualEscalation" : request.Reason.Trim();
        }, cancellationToken);
    }

    public Task<ApiResponse<ConversationDetailResponse>> EnableHumanTakeoverAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        return TransitionAsync(conversationId, ConversationStatus.HumanManaged, "HumanTakeoverEnabled", conversation =>
        {
            conversation.HumanTakeoverEnabled = true;
            // Keep an explicit assignment when host takes ownership for accountability.
            if (currentTenantContext.UserId is { } userId && userId != Guid.Empty)
            {
                conversation.AssignedUserId = userId;
            }
        }, cancellationToken);
    }

    public Task<ApiResponse<ConversationDetailResponse>> ReturnToAIModeAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        return TransitionAsync(conversationId, ConversationStatus.Open, "ReturnedToAI", conversation =>
        {
            // Keep assignment for audit/history even after returning to AI mode.
            conversation.HumanTakeoverEnabled = false;
        }, cancellationToken);
    }

    public Task<ApiResponse<ConversationDetailResponse>> ResolveConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        return TransitionAsync(conversationId, ConversationStatus.Resolved, "ConversationResolved", _ => { }, cancellationToken);
    }

    public Task<ApiResponse<ConversationDetailResponse>> CloseConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        return TransitionAsync(conversationId, ConversationStatus.Closed, "ConversationClosed", conversation =>
        {
            conversation.ClosedAt = DateTimeOffset.UtcNow;
            conversation.HumanTakeoverEnabled = false;
        }, cancellationToken);
    }

    public async Task<ApiResponse<ConversationDetailResponse>> AssignConversationToCurrentUserAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await GetConversationForTenantAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Conversation was not found.");
        }

        if (currentTenantContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Authenticated user context is required.");
        }

        var user = await conversationRepository.GetUserAsync(conversation.CompanyId, userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Assigned user was not found.");
        }

        conversation.AssignedUserId = userId;
        conversation.HumanTakeoverEnabled = true;
        conversation.Status = ConversationStatus.HumanManaged;
        conversation.LastActivityAt = DateTimeOffset.UtcNow;

        await AuditAsync(conversation.CompanyId, conversation.Id, "ConversationAssigned", conversation.Status, null, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        var refreshedConversation = await conversationRepository.GetByIdForCompanyAsync(conversation.CompanyId, conversation.Id, cancellationToken) ?? conversation;
        var payload = new
        {
            conversationId = conversation.Id,
            assignedUser = new { id = user.Id, fullName = user.FullName },
            timestamp = DateTimeOffset.UtcNow
        };
        await realtimePublisher.PublishConversationAssignedAsync(conversation.CompanyId, conversation.Id, payload, cancellationToken);

        return ApiResponse<ConversationDetailResponse>.Ok(MapDetail(refreshedConversation), "Conversation assigned successfully.");
    }

    public async Task<ApiResponse<ConversationDetailResponse>> UnassignConversationAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await GetConversationForTenantAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Conversation was not found.");
        }

        conversation.AssignedUserId = null;
        conversation.LastActivityAt = DateTimeOffset.UtcNow;

        await AuditAsync(conversation.CompanyId, conversation.Id, "ConversationUnassigned", conversation.Status, null, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        await realtimePublisher.PublishConversationAssignedAsync(conversation.CompanyId, conversation.Id, new
        {
            conversationId = conversation.Id,
            assignedUser = (object?)null,
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

        return ApiResponse<ConversationDetailResponse>.Ok(MapDetail(conversation), "Conversation unassigned successfully.");
    }

    public async Task<ApiResponse<bool>> MarkConversationReadForCurrentUserAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (currentTenantContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return ApiResponse<bool>.Fail("Authenticated user context is required.");
        }

        var result = await MarkReadStateAsync(conversationId, ConversationParticipantKind.HostUser, userId, cancellationToken);
        return result;
    }

    public async Task<ApiResponse<bool>> MarkConversationReadForGuestAsync(Guid conversationId, Guid guestId, CancellationToken cancellationToken)
    {
        if (guestId == Guid.Empty)
        {
            return ApiResponse<bool>.Fail("GuestId is required.");
        }

        var conversation = await GetConversationForTenantAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<bool>.Fail("Conversation was not found.");
        }

        if (conversation.GuestId != guestId)
        {
            return ApiResponse<bool>.Fail("Conversation guest identity conflicts with the supplied guest identity.");
        }

        return await MarkReadStateAsync(conversationId, ConversationParticipantKind.Guest, guestId, cancellationToken);
    }

    private async Task<ApiResponse<ConversationMessageResponse>> AddMessageAsync(
        Guid conversationId,
        ConversationSenderType senderType,
        ConversationMessageType messageType,
        string content,
        DateTimeOffset? sentAt,
        string? externalMessageId,
        AIOrchestrationResult? aiResult,
        bool isInternal,
        string auditAction,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ConversationMessageResponse>.Fail(tenantError, [tenantError]);
        }

        var errors = ValidateContent(content);
        if (errors.Count > 0)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Conversation message validation failed.", errors);
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Conversation was not found.");
        }

        if (!transitionPolicy.CanStoreMessage(conversation, senderType))
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Conversation state does not allow this message.");
        }

        var normalizedSentAt = (sentAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConversationId = conversation.Id,
            SenderType = senderType,
            Content = content.Trim(),
            MessageType = messageType,
            ExternalMessageId = NormalizeIdentity(externalMessageId),
            ProviderName = aiResult?.ProviderMetadata?.ProviderName,
            ProviderModel = aiResult?.ProviderMetadata?.ModelName,
            ProviderRequestId = aiResult?.ProviderMetadata?.RequestId,
            AIOutcome = aiResult?.Outcome.ToString(),
            EscalationReason = aiResult?.EscalationReason,
            IsInternal = isInternal,
            SentAt = normalizedSentAt
        };

        conversation.LastActivityAt = normalizedSentAt;
        if (senderType == ConversationSenderType.Guest && conversation.Status == ConversationStatus.Resolved && options.Value.AllowResolvedConversationReopen)
        {
            conversation.Status = ConversationStatus.Open;
            conversation.ClosedAt = null;
        }

        await conversationRepository.AddMessageAsync(message, cancellationToken);
        await AuditAsync(companyId, conversation.Id, auditAction, conversation.Status, senderType, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        await realtimePublisher.PublishMessageCreatedAsync(companyId, conversation.Id, new
        {
            conversationId = conversation.Id,
            message = MapMessage(message),
            isInternal,
            timestamp = DateTimeOffset.UtcNow
        }, isInternal, cancellationToken);

        await realtimePublisher.PublishConversationUnreadCountChangedAsync(companyId, new
        {
            conversationId = conversation.Id,
            senderType = senderType.ToString(),
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

        return ApiResponse<ConversationMessageResponse>.Ok(MapMessage(message), "Conversation message stored successfully.");
    }

    private async Task<ApiResponse<ConversationDetailResponse>> TransitionAsync(Guid conversationId, ConversationStatus targetStatus, string auditAction, Action<Conversation> mutate, CancellationToken cancellationToken)
    {
        var conversation = await GetConversationForTenantAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Conversation was not found.");
        }

        if (!transitionPolicy.CanTransition(conversation.Status, targetStatus))
        {
            return ApiResponse<ConversationDetailResponse>.Fail("Conversation status transition failed.", ["Invalid conversation status transition."]);
        }

        conversation.Status = targetStatus;
        conversation.LastActivityAt = DateTimeOffset.UtcNow;
        mutate(conversation);
        await AuditAsync(conversation.CompanyId, conversation.Id, auditAction, conversation.Status, null, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        if (auditAction is "HumanTakeoverEnabled" or "ReturnedToAI")
        {
            var assignedUser = conversation.AssignedUserId.HasValue
                ? await conversationRepository.GetUserAsync(conversation.CompanyId, conversation.AssignedUserId.Value, cancellationToken)
                : null;
            await realtimePublisher.PublishConversationAssignedAsync(conversation.CompanyId, conversation.Id, new
            {
                conversationId = conversation.Id,
                assignedUser = assignedUser is null
                    ? null
                    : new { id = assignedUser.Id, fullName = assignedUser.FullName },
                humanTakeoverEnabled = conversation.HumanTakeoverEnabled,
                status = conversation.Status.ToString(),
                timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        return ApiResponse<ConversationDetailResponse>.Ok(MapDetail(conversation), "Conversation updated successfully.");
    }

    private async Task<ApiResponse<bool>> MarkReadStateAsync(
        Guid conversationId,
        ConversationParticipantKind participantKind,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<bool>.Fail(tenantError, [tenantError]);
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<bool>.Fail("Conversation was not found.");
        }

        var latestVisibleMessage = await conversationRepository.GetLatestVisibleMessageAsync(companyId, conversationId, cancellationToken);

        var readAt = latestVisibleMessage?.SentAt ?? DateTimeOffset.UtcNow;
        var currentState = await conversationRepository.GetReadStateAsync(companyId, conversationId, participantKind, participantId, cancellationToken);
        if (currentState is null)
        {
            await conversationRepository.AddReadStateAsync(new ConversationParticipantReadState
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ConversationId = conversationId,
                ParticipantKind = participantKind,
                ParticipantId = participantId,
                LastReadMessageId = latestVisibleMessage?.Id,
                LastReadAt = readAt
            }, cancellationToken);
        }
        else
        {
            currentState.LastReadAt = readAt;
            currentState.LastReadMessageId = latestVisibleMessage?.Id;
        }

        await conversationRepository.SaveChangesAsync(cancellationToken);

        await realtimePublisher.PublishConversationReadStateChangedAsync(companyId, conversationId, new
        {
            conversationId,
            participantKind = participantKind.ToString(),
            participantId,
            lastReadAt = readAt,
            lastReadMessageId = latestVisibleMessage?.Id,
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

        await realtimePublisher.PublishConversationUnreadCountChangedAsync(companyId, new
        {
            conversationId,
            participantKind = participantKind.ToString(),
            participantId,
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

        return ApiResponse<bool>.Ok(true, "Conversation marked as read.");
    }

    private async Task<ApiResponse<AssociationValidationResult>> ValidateConversationAssociationsAsync(Guid companyId, CreateConversationRequest request, CancellationToken cancellationToken)
    {
        if (request.GuestId == Guid.Empty)
        {
            return ApiResponse<AssociationValidationResult>.Fail("Conversation validation failed.", ["GuestId is required."]);
        }

        var guest = await conversationRepository.GetGuestAsync(companyId, request.GuestId, cancellationToken);
        if (guest is null)
        {
            return ApiResponse<AssociationValidationResult>.Fail("Guest was not found.");
        }

        Guid? reservationPropertyId = null;
        if (request.ReservationId is { } reservationId)
        {
            var reservation = await conversationRepository.GetReservationAsync(companyId, reservationId, cancellationToken);
            if (reservation is null || reservation.PrimaryGuestId != request.GuestId)
            {
                return ApiResponse<AssociationValidationResult>.Fail("Reservation was not found.");
            }

            reservationPropertyId = reservation.PropertyId;
        }

        if (request.PropertyId is { } propertyId && await conversationRepository.GetPropertyAsync(companyId, propertyId, cancellationToken) is null)
        {
            return ApiResponse<AssociationValidationResult>.Fail("Property was not found.");
        }

        if (request.AssignedUserId is { } assignedUserId && await conversationRepository.GetUserAsync(companyId, assignedUserId, cancellationToken) is null)
        {
            return ApiResponse<AssociationValidationResult>.Fail("Assigned user was not found.");
        }

        return ApiResponse<AssociationValidationResult>.Ok(new AssociationValidationResult(reservationPropertyId));
    }

    private async Task<Conversation?> GetConversationForTenantAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out _))
        {
            return null;
        }

        return conversationId == Guid.Empty
            ? null
            : await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
    }

    private async Task<ConversationMessage?> FindDuplicateAsync(string? externalMessageId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalMessageId) || !TryGetCompanyId(out var companyId, out _))
        {
            return null;
        }

        return await conversationRepository.FindByExternalMessageIdAsync(companyId, externalMessageId.Trim(), cancellationToken);
    }

    private IReadOnlyCollection<string> ValidateContent(string content)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add("Content is required.");
        }
        else if (content.Trim().Length > options.Value.MaxMessageCharacters)
        {
            errors.Add($"Content must be {options.Value.MaxMessageCharacters} characters or fewer.");
        }

        return errors;
    }

    private static IReadOnlyCollection<string> ValidateListQuery(ConversationListQueryParameters query)
    {
        var errors = new List<string>();
        if (query.Page < 1)
        {
            errors.Add("Page must be greater than or equal to 1.");
        }

        if (query.PageSize < 1)
        {
            errors.Add("PageSize must be greater than or equal to 1.");
        }
        else if (query.PageSize > 100)
        {
            errors.Add("PageSize must be 100 or fewer.");
        }

        if (query.PropertyId == Guid.Empty)
        {
            errors.Add("PropertyId must be a valid identifier.");
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

    private async Task AuditAsync(Guid companyId, Guid conversationId, string action, ConversationStatus status, ConversationSenderType? senderType, CancellationToken cancellationToken)
    {
        await conversationRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "Conversation",
            EntityId = conversationId,
            Action = action,
            Details = JsonSerializer.Serialize(new
            {
                currentTenantContext.CorrelationId,
                CompanyId = companyId,
                ConversationId = conversationId,
                Status = status.ToString(),
                SenderType = senderType?.ToString(),
                Timestamp = DateTimeOffset.UtcNow
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static string? NormalizeIdentity(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ConversationDetailResponse MapDetail(Conversation conversation)
    {
        return new ConversationDetailResponse
        {
            Id = conversation.Id,
            ConversationId = conversation.Id,
            GuestId = conversation.GuestId,
            ReservationId = conversation.ReservationId,
            PropertyId = conversation.PropertyId,
            Channel = conversation.Channel,
            ChannelIdentity = conversation.ChannelIdentity,
            Status = conversation.Status,
            Subject = conversation.Subject,
            HumanTakeoverEnabled = conversation.HumanTakeoverEnabled,
            RequiresHostAttention = IsHostAttentionConversation(conversation),
            EscalationReason = conversation.EscalationReason,
            StartedAt = conversation.StartedAt,
            LastActivityAt = conversation.LastActivityAt,
            ClosedAt = conversation.ClosedAt,
            Guest = new ConversationGuestSummary
            {
                Id = conversation.GuestId,
                FirstName = conversation.Guest?.FirstName ?? string.Empty,
                LastName = conversation.Guest?.LastName ?? string.Empty,
                FullName = conversation.Guest is null ? string.Empty : $"{conversation.Guest.FirstName} {conversation.Guest.LastName}".Trim(),
                Email = conversation.Guest?.Email,
                PreferredLanguage = conversation.Guest?.PreferredLanguage ?? string.Empty
            },
            Reservation = conversation.Reservation is null
                ? null
                : new ConversationReservationSummary
                {
                    Id = conversation.Reservation.Id,
                    ConfirmationNumber = conversation.Reservation.ConfirmationNumber,
                    CheckInDate = conversation.Reservation.CheckInDate,
                    CheckOutDate = conversation.Reservation.CheckOutDate,
                    Status = conversation.Reservation.Status
                },
            Property = conversation.Property is null
                ? null
                : new ConversationPropertySummary
                {
                    Id = conversation.Property.Id,
                    Name = conversation.Property.Name,
                    City = conversation.Property.City
                },
            AssignedUser = conversation.AssignedUser is null
                ? null
                : new ConversationAssignedUserSummary
                {
                    Id = conversation.AssignedUser.Id,
                    FullName = conversation.AssignedUser.FullName
                },
            Messages = conversation.Messages.Where(message => !message.IsInternal).OrderBy(message => message.SentAt).Select(MapMessage).ToList()
        };
    }

    private static ConversationMessageResponse MapMessage(ConversationMessage message)
    {
        return new ConversationMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderType = message.SenderType,
            MessageType = message.MessageType,
            Content = message.Content,
            IsInternal = message.IsInternal,
            SentAt = message.SentAt
        };
    }

    private static bool IsHostAttentionConversation(Conversation conversation)
    {
        return conversation.HumanTakeoverEnabled
            || conversation.Status == ConversationStatus.AwaitingHost
            || conversation.Status == ConversationStatus.Escalated
            || conversation.Status == ConversationStatus.HumanManaged;
    }

    private static ConversationSummaryResponse EnrichSummaryWithReadData(ConversationSummaryResponse item, int unreadCount, DateTimeOffset? lastReadAt)
    {
        return new ConversationSummaryResponse
        {
            Id = item.Id,
            ConversationId = item.ConversationId,
            GuestId = item.GuestId,
            ReservationId = item.ReservationId,
            PropertyId = item.PropertyId,
            Channel = item.Channel,
            ChannelIdentity = item.ChannelIdentity,
            Status = item.Status,
            Subject = item.Subject,
            HumanTakeoverEnabled = item.HumanTakeoverEnabled,
            RequiresHostAttention = item.RequiresHostAttention,
            EscalationReason = item.EscalationReason,
            StartedAt = item.StartedAt,
            LastActivityAt = item.LastActivityAt,
            ClosedAt = item.ClosedAt,
            Guest = item.Guest,
            Reservation = item.Reservation,
            Property = item.Property,
            AssignedUser = item.AssignedUser,
            LatestVisibleMessagePreview = item.LatestVisibleMessagePreview,
            LatestVisibleMessageSenderType = item.LatestVisibleMessageSenderType,
            LatestVisibleMessageTimestamp = item.LatestVisibleMessageTimestamp,
            TotalVisibleMessageCount = item.TotalVisibleMessageCount,
            UnreadMessageCount = unreadCount,
            LastReadAt = lastReadAt
        };
    }

    private sealed record AssociationValidationResult(Guid? ReservationPropertyId);
}
