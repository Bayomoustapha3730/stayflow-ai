using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.Copilot;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Orchestration;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Services.HostCopilot;

public sealed class HostCopilotWorkspaceService(
    ApplicationDbContext dbContext,
    ICurrentTenantContext tenantContext,
    IConversationService conversationService,
    IConciergeHostActionService hostActionService,
    IAIReplyOrchestrator replyOrchestrator,
    IConversationRealtimePublisher realtimePublisher,
    IOptions<HostCopilotOptions> options) : IHostCopilotWorkspaceService
{
    private static readonly string[] EmergencyKeywords =
    [
        "fire", "smoke", "gas leak", "burglary", "break in", "bleeding", "injury", "ambulance", "unsafe",
        "electrical shock", "police", "help immediately", "flooding", "no power", "carbon monoxide"
    ];

    private static readonly string[] HighPriorityKeywords =
    [
        "locked out", "cannot enter", "can't enter", "no water", "hot water", "heater", "aircon", "ac not working",
        "security", "urgent", "medical", "allergic", "lost key"
    ];

    private const int MaxDecisionNoteLength = 180;

    public async Task<ApiResponse<HostCopilotWorkspaceResponse>> GetWorkspaceAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        if (!TryGetTenantContext(out var companyId, out var userId, out var tenantError))
        {
            return ApiResponse<HostCopilotWorkspaceResponse>.Fail(tenantError, [tenantError]);
        }

        if (propertyId.HasValue)
        {
            var propertyExists = await dbContext.Properties.AsNoTracking()
                .AnyAsync(property => property.CompanyId == companyId && property.Id == propertyId.Value, cancellationToken);
            if (!propertyExists)
            {
                return ApiResponse<HostCopilotWorkspaceResponse>.Fail("Property was not found for the current tenant.");
            }
        }

        var conversationsQuery = dbContext.Conversations
            .AsNoTracking()
            .Include(conversation => conversation.Guest)
            .Include(conversation => conversation.Property)
            .Where(conversation => conversation.CompanyId == companyId && !conversation.IsDeleted)
            .Where(conversation => conversation.Status != ConversationStatus.Closed);

        if (propertyId.HasValue)
        {
            conversationsQuery = conversationsQuery.Where(conversation => conversation.PropertyId == propertyId.Value);
        }

        var conversations = await conversationsQuery
            .OrderByDescending(conversation => conversation.LastActivityAt)
            .Take(40)
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return ApiResponse<HostCopilotWorkspaceResponse>.Ok(new HostCopilotWorkspaceResponse
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                TotalOpenItems = 0,
                TotalBreachedSlaItems = 0,
                Items = []
            });
        }

        var conversationIds = conversations.Select(conversation => conversation.Id).ToArray();

        var messages = await dbContext.ConversationMessages.AsNoTracking()
            .Where(message => message.CompanyId == companyId && conversationIds.Contains(message.ConversationId) && !message.IsDeleted)
            .OrderBy(message => message.SentAt)
            .ToListAsync(cancellationToken);

        var pendingActions = await dbContext.PendingConciergeActions.AsNoTracking()
            .Where(action => action.CompanyId == companyId
                && conversationIds.Contains(action.ConversationId)
                && action.Status == PendingConciergeActionStatus.AwaitingHostApproval)
            .OrderByDescending(action => action.CreatedAt)
            .ToListAsync(cancellationToken);

        var auditEvents = await dbContext.ConciergeActionAuditLogs.AsNoTracking()
            .Where(audit => audit.CompanyId == companyId && conversationIds.Contains(audit.ConversationId))
            .OrderByDescending(audit => audit.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var activeAlerts = await dbContext.HostCopilotSlaAlerts
            .Where(alert => alert.CompanyId == companyId && conversationIds.Contains(alert.ConversationId) && alert.Status == HostCopilotSlaAlertStatus.Open)
            .ToListAsync(cancellationToken);

        var messagesByConversation = messages
            .GroupBy(message => message.ConversationId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ConversationMessage>)group.ToList());

        var pendingActionsByConversation = pendingActions
            .GroupBy(action => action.ConversationId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PendingConciergeAction>)group.ToList());

        var auditEventsByConversation = auditEvents
            .GroupBy(audit => audit.ConversationId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ConciergeActionAuditLog>)group.ToList());

        var openAlertsByConversation = activeAlerts
            .GroupBy(alert => alert.ConversationId)
            .ToDictionary(group => group.Key, group => group.First());

        var workItems = new List<HostCopilotWorkItemDto>(conversations.Count);
        var now = DateTimeOffset.UtcNow;

        foreach (var conversation in conversations)
        {
            var conversationMessages = messagesByConversation.TryGetValue(conversation.Id, out var messageItems)
                ? messageItems
                : [];
            var visibleMessages = conversationMessages.Where(message => !message.IsInternal && message.MessageType != ConversationMessageType.InternalNote).ToList();
            var latestGuestMessage = visibleMessages.LastOrDefault(message => message.SenderType == ConversationSenderType.Guest);
            var latestHostMessage = visibleMessages.LastOrDefault(message => message.SenderType == ConversationSenderType.Host);
            var conversationActions = pendingActionsByConversation.TryGetValue(conversation.Id, out var actionItems)
                ? actionItems
                : [];
            var conversationAudits = auditEventsByConversation.TryGetValue(conversation.Id, out var auditItems)
                ? auditItems
                : [];

            var safety = ClassifySafety(latestGuestMessage?.Content, conversationActions.Count);
            var priority = ClassifyPriority(latestGuestMessage?.Content, safety.IsEmergency, conversationActions.Count, conversation.LastActivityAt);
            var sla = BuildSlaStatus(priority, latestGuestMessage?.SentAt, latestHostMessage?.SentAt, now);

            await UpsertSlaAlertAsync(
                companyId,
                conversation,
                priority,
                safety.IsEmergency,
                sla,
                latestGuestMessage?.SentAt,
                openAlertsByConversation,
                cancellationToken);

            var workItem = new HostCopilotWorkItemDto
            {
                WorkItemId = conversation.Id,
                ConversationId = conversation.Id,
                PropertyId = conversation.PropertyId ?? Guid.Empty,
                ReservationId = conversation.ReservationId,
                PropertyName = conversation.Property?.Name ?? "Unknown property",
                GuestName = BuildGuestDisplayName(conversation.Guest),
                Priority = priority.Priority.ToString(),
                IsEmergency = safety.IsEmergency,
                SafetyClassification = safety.Classification,
                PriorityReason = priority.Reason,
                Sla = sla,
                Summary = BuildOperationalSummary(conversation, latestGuestMessage, conversationActions.Count, visibleMessages.Count),
                Timeline = BuildTimeline(visibleMessages, conversationAudits.Take(options.Value.MaximumTimelineEvents).ToList(), options.Value.MaximumTimelineEvents),
                Recommendations = BuildRecommendations(conversation, safety, priority, latestGuestMessage?.Content, conversationActions.Count),
                PendingActions = conversationActions.Select(action => new HostCopilotPendingActionDto
                {
                    ActionId = action.Id,
                    ActionType = action.ActionType.ToString(),
                    Status = action.Status.ToString(),
                    CreatedAt = action.CreatedAt,
                    ExpiresAt = action.ExpiresAt
                }).ToList()
            };

            workItems.Add(workItem);
        }

        var response = new HostCopilotWorkspaceResponse
        {
            GeneratedAt = now,
            TotalOpenItems = workItems.Count,
            TotalBreachedSlaItems = workItems.Count(item => item.Sla.IsBreached),
            Items = workItems
                .OrderByDescending(item => item.IsEmergency)
                .ThenByDescending(item => Enum.Parse<HostNotificationPriority>(item.Priority))
                .ThenByDescending(item => item.Sla.IsBreached)
                .ThenBy(item => item.Sla.ResponseDueAt)
                .ToList()
        };

        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<HostCopilotWorkspaceResponse>.Ok(response);
    }

    public async Task<ApiResponse<HostCopilotDraftResponse>> GenerateDraftAsync(Guid conversationId, HostCopilotDraftGenerateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenantContext(out var companyId, out _, out var tenantError))
        {
            return ApiResponse<HostCopilotDraftResponse>.Fail(tenantError, [tenantError]);
        }

        var conversation = await dbContext.Conversations.AsNoTracking()
            .Include(item => item.Guest)
            .Include(item => item.Property)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Id == conversationId && !item.IsDeleted, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<HostCopilotDraftResponse>.Fail("Conversation was not found.");
        }

        var latestGuestMessage = await dbContext.ConversationMessages.AsNoTracking()
            .Where(message => message.CompanyId == companyId
                && message.ConversationId == conversationId
                && message.SenderType == ConversationSenderType.Guest
                && !message.IsInternal
                && !message.IsDeleted)
            .OrderByDescending(message => message.SentAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestGuestMessage is null)
        {
            return ApiResponse<HostCopilotDraftResponse>.Fail("No guest message is available for draft generation.");
        }

        string draft;
        var deterministicFallback = false;
        var mode = "llm";
        var rationale = "Generated from tenant-scoped conversation context.";

        if (options.Value.EnableLlmWording)
        {
            var orchestrated = await replyOrchestrator.OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
            {
                ConversationId = conversationId,
                Operation = AIReplyOperation.GeneratedHostReply,
                RequestedTone = request.Tone,
                HostInstruction = request.HostInstruction,
                CorrelationId = tenantContext.CorrelationId
            }, cancellationToken);

            if (orchestrated is null)
            {
                return ApiResponse<HostCopilotDraftResponse>.Fail("Conversation was not found.");
            }

            deterministicFallback = orchestrated.FallbackUsed || string.IsNullOrWhiteSpace(orchestrated.Output);
            draft = deterministicFallback
                ? BuildDeterministicDraft(conversation, latestGuestMessage.Content, request.Tone)
                : orchestrated.Output!.Trim();

            if (deterministicFallback)
            {
                mode = "deterministic-fallback";
                rationale = "Deterministic fallback was used because orchestration safeguards required safe wording.";
            }
        }
        else
        {
            deterministicFallback = true;
            mode = "deterministic";
            rationale = "LLM wording is disabled by configuration. Deterministic wording was used.";
            draft = BuildDeterministicDraft(conversation, latestGuestMessage.Content, request.Tone);
        }

        var validation = ValidateDraftInternal(draft);
        return ApiResponse<HostCopilotDraftResponse>.Ok(new HostCopilotDraftResponse
        {
            ConversationId = conversationId,
            Draft = draft,
            UsedDeterministicFallback = deterministicFallback,
            GenerationMode = mode,
            Rationale = rationale,
            Validation = validation
        });
    }

    public Task<ApiResponse<HostCopilotDraftValidationResponse>> ValidateDraftAsync(Guid conversationId, HostCopilotDraftValidateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenantContext(out _, out _, out var tenantError))
        {
            return Task.FromResult(ApiResponse<HostCopilotDraftValidationResponse>.Fail(tenantError, [tenantError]));
        }

        _ = conversationId;
        _ = cancellationToken;

        var validation = ValidateDraftInternal(request.Draft);
        return Task.FromResult(ApiResponse<HostCopilotDraftValidationResponse>.Ok(validation));
    }

    public async Task<ApiResponse<ConversationMessageResponse>> SendDraftAsync(Guid conversationId, HostCopilotDraftSendRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenantContext(out var companyId, out _, out var tenantError))
        {
            return ApiResponse<ConversationMessageResponse>.Fail(tenantError, [tenantError]);
        }

        var conversationExists = await dbContext.Conversations.AsNoTracking()
            .AnyAsync(conversation => conversation.CompanyId == companyId && conversation.Id == conversationId && !conversation.IsDeleted, cancellationToken);
        if (!conversationExists)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Conversation was not found.");
        }

        var validation = ValidateDraftInternal(request.Draft);
        if (!validation.IsValid)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Draft validation failed.", validation.Errors.ToList());
        }

        var response = await conversationService.AddHostMessageAsync(conversationId, new AddHostMessageRequest
        {
            Content = request.Draft,
            SentAt = DateTimeOffset.UtcNow,
            Provider = ConversationMessageProvider.None
        }, cancellationToken);

        if (!response.Success)
        {
            return response;
        }

        await ResolveOpenAlertsAsync(companyId, conversationId, cancellationToken);

        await realtimePublisher.PublishHostCopilotWorkspaceUpdatedAsync(companyId, new
        {
            conversationId,
            eventType = "DraftSent",
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

        return response;
    }

    public async Task<ApiResponse<HostActionListItem>> ApprovePendingActionAsync(Guid actionId, HostActionDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenantContext(out var companyId, out var userId, out var tenantError))
        {
            return ApiResponse<HostActionListItem>.Fail(tenantError, [tenantError]);
        }

        var decisionValidation = ValidateDecisionRequest(request);
        if (decisionValidation is not null)
        {
            return ApiResponse<HostActionListItem>.Fail(decisionValidation, [decisionValidation]);
        }

        var response = await hostActionService.ApproveAsync(companyId, actionId, userId, request.DecisionNote, cancellationToken);
        if (response.Success)
        {
            await realtimePublisher.PublishHostCopilotWorkspaceUpdatedAsync(companyId, new
            {
                actionId,
                eventType = "ActionApproved",
                timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        return response;
    }

    public async Task<ApiResponse<HostActionListItem>> DeclinePendingActionAsync(Guid actionId, HostActionDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenantContext(out var companyId, out var userId, out var tenantError))
        {
            return ApiResponse<HostActionListItem>.Fail(tenantError, [tenantError]);
        }

        var decisionValidation = ValidateDecisionRequest(request);
        if (decisionValidation is not null)
        {
            return ApiResponse<HostActionListItem>.Fail(decisionValidation, [decisionValidation]);
        }

        var response = await hostActionService.DeclineAsync(companyId, actionId, userId, request.DecisionNote, cancellationToken);
        if (response.Success)
        {
            await realtimePublisher.PublishHostCopilotWorkspaceUpdatedAsync(companyId, new
            {
                actionId,
                eventType = "ActionDeclined",
                timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        return response;
    }

    private async Task ResolveOpenAlertsAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        var alerts = await dbContext.HostCopilotSlaAlerts
            .Where(alert => alert.CompanyId == companyId
                && alert.ConversationId == conversationId
                && alert.Status == HostCopilotSlaAlertStatus.Open)
            .ToListAsync(cancellationToken);

        if (alerts.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var alert in alerts)
        {
            alert.Status = HostCopilotSlaAlertStatus.Resolved;
            alert.ResolvedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertSlaAlertAsync(
        Guid companyId,
        Conversation conversation,
        PriorityResult priority,
        bool isEmergency,
        HostCopilotSlaStatusDto sla,
        DateTimeOffset? latestGuestAt,
        IDictionary<Guid, HostCopilotSlaAlert> openAlertsByConversation,
        CancellationToken cancellationToken)
    {
        if (latestGuestAt is null || !sla.IsBreached || sla.ResponseDueAt is null)
        {
            return;
        }

        openAlertsByConversation.TryGetValue(conversation.Id, out var existing);

        if (existing is not null)
        {
            existing.Priority = priority.Priority;
            existing.IsEmergency = isEmergency;
            existing.DueAt = sla.ResponseDueAt.Value;
            existing.LastGuestMessageAt = latestGuestAt.Value;
            existing.Reason = sla.AlertMessage;
            return;
        }

        var created = new HostCopilotSlaAlert
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConversationId = conversation.Id,
            PropertyId = conversation.PropertyId ?? Guid.Empty,
            ReservationId = conversation.ReservationId,
            Priority = priority.Priority,
            IsEmergency = isEmergency,
            TriggeredAt = DateTimeOffset.UtcNow,
            DueAt = sla.ResponseDueAt.Value,
            LastGuestMessageAt = latestGuestAt.Value,
            Reason = sla.AlertMessage,
            Status = HostCopilotSlaAlertStatus.Open
        };

        await dbContext.HostCopilotSlaAlerts.AddAsync(created, cancellationToken);
        openAlertsByConversation[conversation.Id] = created;
    }

    private HostCopilotOperationalSummaryDto BuildOperationalSummary(
        Conversation conversation,
        ConversationMessage? latestGuestMessage,
        int openActionCount,
        int visibleMessageCount)
    {
        var preview = latestGuestMessage?.Content?.Trim() ?? "No recent guest message.";
        if (preview.Length > 170)
        {
            preview = preview[..167] + "...";
        }

        return new HostCopilotOperationalSummaryDto
        {
            Headline = $"{BuildGuestDisplayName(conversation.Guest)} at {conversation.Property?.Name ?? "property"}",
            LastGuestIntent = DeriveIntentLabel(preview),
            LastGuestMessagePreview = preview,
            OpenActionCount = openActionCount,
            VisibleMessageCount = visibleMessageCount,
            LastActivityAt = conversation.LastActivityAt
        };
    }

    private static IReadOnlyCollection<HostCopilotTimelineEventDto> BuildTimeline(
        IReadOnlyCollection<ConversationMessage> messages,
        IReadOnlyCollection<ConciergeActionAuditLog> audits,
        int maxEvents)
    {
        var messageEvents = messages
            .OrderByDescending(message => message.SentAt)
            .Take(maxEvents)
            .Select(message => new HostCopilotTimelineEventDto
            {
                Timestamp = message.SentAt,
                EventType = "Message",
                Title = $"{message.SenderType} message",
                Detail = Clip(message.Content, 150)
            });

        var auditEvents = audits
            .Select(audit => new HostCopilotTimelineEventDto
            {
                Timestamp = audit.CreatedAt,
                EventType = "Action",
                Title = audit.EventType.ToString(),
                Detail = BuildAuditDetail(audit)
            });

        return messageEvents
            .Concat(auditEvents)
            .OrderByDescending(item => item.Timestamp)
            .Take(maxEvents)
            .ToList();
    }

    private static string BuildAuditDetail(ConciergeActionAuditLog audit)
    {
        if (string.IsNullOrWhiteSpace(audit.MetadataJson))
        {
            return audit.ResultCode;
        }

        return $"{audit.ResultCode}: {Clip(audit.MetadataJson, 120)}";
    }

    private IReadOnlyCollection<HostCopilotRecommendationDto> BuildRecommendations(
        Conversation conversation,
        SafetyResult safety,
        PriorityResult priority,
        string? latestGuestText,
        int openActions)
    {
        var recommendations = new List<HostCopilotRecommendationDto>();

        if (safety.IsEmergency)
        {
            recommendations.Add(new HostCopilotRecommendationDto
            {
                Code = "EmergencyEscalation",
                Title = "Escalate to emergency protocol",
                Reason = "Safety keywords were detected in the latest guest message.",
                SuggestedAction = "Acknowledge immediately and provide emergency contact instructions.",
                Confidence = 96
            });
        }

        if (priority.Priority is HostNotificationPriority.Urgent or HostNotificationPriority.High)
        {
            recommendations.Add(new HostCopilotRecommendationDto
            {
                Code = "RapidResponse",
                Title = "Respond within SLA",
                Reason = priority.Reason,
                SuggestedAction = "Send a short confirmation now, then follow up with details.",
                Confidence = 90
            });
        }

        if (openActions > 0)
        {
            recommendations.Add(new HostCopilotRecommendationDto
            {
                Code = "PendingApprovals",
                Title = "Review pending host approvals",
                Reason = $"{openActions} concierge action(s) are waiting for host input.",
                SuggestedAction = "Approve or decline queued actions to unblock operations.",
                Confidence = 88
            });
        }

        if (!conversation.HumanTakeoverEnabled)
        {
            recommendations.Add(new HostCopilotRecommendationDto
            {
                Code = "TakeoverHint",
                Title = "Enable human takeover for manual handling",
                Reason = "Conversation is still AI-managed while host intervention appears required.",
                SuggestedAction = "Enable human takeover before sending manual updates.",
                Confidence = 75
            });
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(new HostCopilotRecommendationDto
            {
                Code = "Monitor",
                Title = "Continue monitoring",
                Reason = "No urgent or safety signals were detected.",
                SuggestedAction = "Keep the conversation in watch mode and respond on next guest update.",
                Confidence = 65
            });
        }

        if (!string.IsNullOrWhiteSpace(latestGuestText) && latestGuestText.Contains("thank", StringComparison.OrdinalIgnoreCase))
        {
            recommendations.Add(new HostCopilotRecommendationDto
            {
                Code = "ClosureOpportunity",
                Title = "Offer closure",
                Reason = "Guest language suggests the issue may already be resolved.",
                SuggestedAction = "Confirm resolution and ask whether anything else is needed.",
                Confidence = 72
            });
        }

        return recommendations;
    }

    private HostCopilotSlaStatusDto BuildSlaStatus(
        PriorityResult priority,
        DateTimeOffset? latestGuestAt,
        DateTimeOffset? latestHostAt,
        DateTimeOffset now)
    {
        if (latestGuestAt is null)
        {
            return new HostCopilotSlaStatusDto
            {
                MinutesSinceLatestGuestMessage = 0,
                ResponseDueAt = null,
                IsBreached = false,
                AlertLevel = "none",
                AlertMessage = "No guest message pending response."
            };
        }

        var guestAt = latestGuestAt.Value;
        if (latestHostAt.HasValue && latestHostAt.Value >= guestAt)
        {
            return new HostCopilotSlaStatusDto
            {
                MinutesSinceLatestGuestMessage = (int)Math.Max(0, Math.Round((now - guestAt).TotalMinutes)),
                ResponseDueAt = null,
                IsBreached = false,
                AlertLevel = "cleared",
                AlertMessage = "Latest guest message has already been answered by host."
            };
        }

        var slaMinutes = priority.Priority switch
        {
            HostNotificationPriority.Urgent => options.Value.UrgentPrioritySlaMinutes,
            HostNotificationPriority.High => options.Value.HighPrioritySlaMinutes,
            _ => options.Value.NormalPrioritySlaMinutes
        };

        var dueAt = guestAt.AddMinutes(slaMinutes);
        var minutesSinceGuest = (int)Math.Max(0, Math.Round((now - guestAt).TotalMinutes));
        var isBreached = now > dueAt;

        if (isBreached)
        {
            var overBy = (int)Math.Max(1, Math.Round((now - dueAt).TotalMinutes));
            return new HostCopilotSlaStatusDto
            {
                MinutesSinceLatestGuestMessage = minutesSinceGuest,
                ResponseDueAt = dueAt,
                IsBreached = true,
                AlertLevel = "breach",
                AlertMessage = $"SLA breached by {overBy} minute(s)."
            };
        }

        var minutesLeft = (int)Math.Max(0, Math.Round((dueAt - now).TotalMinutes));
        var alertLevel = minutesLeft <= 2 ? "warning" : "ok";
        return new HostCopilotSlaStatusDto
        {
            MinutesSinceLatestGuestMessage = minutesSinceGuest,
            ResponseDueAt = dueAt,
            IsBreached = false,
            AlertLevel = alertLevel,
            AlertMessage = alertLevel == "warning"
                ? $"SLA warning: {minutesLeft} minute(s) remaining."
                : $"SLA healthy: {minutesLeft} minute(s) remaining."
        };
    }

    private static PriorityResult ClassifyPriority(string? latestGuestText, bool emergency, int openActions, DateTimeOffset lastActivityAt)
    {
        if (emergency)
        {
            return new PriorityResult(HostNotificationPriority.Urgent, "Emergency signal detected from guest message.");
        }

        var normalized = latestGuestText?.Trim().ToLowerInvariant() ?? string.Empty;
        if (HighPriorityKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal)))
        {
            return new PriorityResult(HostNotificationPriority.High, "High-priority issue keywords detected.");
        }

        if (openActions >= 2)
        {
            return new PriorityResult(HostNotificationPriority.High, "Multiple pending host approvals require attention.");
        }

        if (DateTimeOffset.UtcNow - lastActivityAt > TimeSpan.FromHours(8))
        {
            return new PriorityResult(HostNotificationPriority.Low, "No recent activity in conversation.");
        }

        return new PriorityResult(HostNotificationPriority.Normal, "Default operational priority.");
    }

    private static SafetyResult ClassifySafety(string? latestGuestText, int openActions)
    {
        var normalized = latestGuestText?.Trim().ToLowerInvariant() ?? string.Empty;
        var isEmergency = EmergencyKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
        if (isEmergency)
        {
            return new SafetyResult(true, "Emergency", "Emergency keyword match");
        }

        if (normalized.Contains("unsafe", StringComparison.Ordinal) || normalized.Contains("danger", StringComparison.Ordinal))
        {
            return new SafetyResult(true, "SafetyConcern", "Guest reported unsafe or dangerous situation");
        }

        if (openActions > 0 && (normalized.Contains("broken", StringComparison.Ordinal) || normalized.Contains("not working", StringComparison.Ordinal)))
        {
            return new SafetyResult(false, "OperationalRisk", "Operational risk inferred from issue and pending actions");
        }

        return new SafetyResult(false, "Normal", "No deterministic emergency or safety risk signal detected");
    }

    private static string BuildDeterministicDraft(Conversation conversation, string latestGuestMessage, string? tone)
    {
        var guestName = BuildGuestDisplayName(conversation.Guest);
        var propertyName = conversation.Property?.Name?.Trim();
        var toneLabel = string.IsNullOrWhiteSpace(tone) ? "professional" : tone.Trim().ToLowerInvariant();

        var acknowledgement = toneLabel switch
        {
            "friendly" => $"Hi {guestName}, thanks for your message.",
            "casual" => $"Hi {guestName}, thanks for reaching out.",
            "luxury" => $"Hello {guestName}, thank you for contacting us.",
            _ => $"Hello {guestName}, thank you for your message."
        };

        var contextLine = string.IsNullOrWhiteSpace(propertyName)
            ? "I am reviewing this now."
            : $"I am reviewing this now with our {propertyName} team.";

        var issueLine = $"You mentioned: \"{Clip(latestGuestMessage.Trim(), 120)}\".";

        return $"{acknowledgement} {issueLine} {contextLine} I will send a precise update shortly.";
    }

    private static HostCopilotDraftValidationResponse ValidateDraftInternal(string draft)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var trimmed = draft?.Trim() ?? string.Empty;
        if (trimmed.Length < 20)
        {
            errors.Add("Draft must be at least 20 characters.");
        }

        if (trimmed.Length > 1500)
        {
            errors.Add("Draft must be 1500 characters or fewer.");
        }

        if (trimmed.Contains("TODO", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("lorem ipsum", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("{{", StringComparison.Ordinal))
        {
            errors.Add("Draft includes placeholder text that must be resolved before sending.");
        }

        if (!trimmed.EndsWith(".", StringComparison.Ordinal)
            && !trimmed.EndsWith("!", StringComparison.Ordinal)
            && !trimmed.EndsWith("?", StringComparison.Ordinal))
        {
            warnings.Add("Draft should end with sentence punctuation for clarity.");
        }

        if (trimmed.Contains("guarantee", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("always", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Avoid absolute promises unless they are guaranteed operationally.");
        }

        return new HostCopilotDraftValidationResponse
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private static string DeriveIntentLabel(string text)
    {
        var normalized = text.ToLowerInvariant();
        if (normalized.Contains("check in", StringComparison.Ordinal) || normalized.Contains("arrival", StringComparison.Ordinal))
        {
            return "Arrival";
        }

        if (normalized.Contains("checkout", StringComparison.Ordinal) || normalized.Contains("check out", StringComparison.Ordinal))
        {
            return "Checkout";
        }

        if (normalized.Contains("wifi", StringComparison.Ordinal))
        {
            return "WiFi";
        }

        if (normalized.Contains("parking", StringComparison.Ordinal))
        {
            return "Parking";
        }

        if (normalized.Contains("clean", StringComparison.Ordinal) || normalized.Contains("housekeeping", StringComparison.Ordinal))
        {
            return "Housekeeping";
        }

        if (normalized.Contains("broken", StringComparison.Ordinal) || normalized.Contains("maintenance", StringComparison.Ordinal))
        {
            return "Maintenance";
        }

        return "General";
    }

    private static string BuildGuestDisplayName(Guest? guest)
    {
        if (guest is null)
        {
            return "Guest";
        }

        var fullName = string.Join(" ", new[] { guest.FirstName, guest.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
        return string.IsNullOrWhiteSpace(fullName) ? "Guest" : fullName;
    }

    private static string Clip(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..(maxLength - 3)] + "...";
    }

    private bool TryGetTenantContext(out Guid companyId, out Guid userId, out string error)
    {
        if (!tenantContext.IsAuthenticated || tenantContext.CompanyId is not { } tenantCompanyId || tenantCompanyId == Guid.Empty)
        {
            companyId = Guid.Empty;
            userId = Guid.Empty;
            error = "Authenticated tenant context is required.";
            return false;
        }

        if (tenantContext.UserId is not { } tenantUserId || tenantUserId == Guid.Empty)
        {
            companyId = Guid.Empty;
            userId = Guid.Empty;
            error = "Authenticated user context is required.";
            return false;
        }

        companyId = tenantCompanyId;
        userId = tenantUserId;
        error = string.Empty;
        return true;
    }

    private static string? ValidateDecisionRequest(HostActionDecisionRequest request)
    {
        if (request is null)
        {
            return "Decision request is required.";
        }

        var note = request.DecisionNote?.Trim();
        if (note is { Length: > MaxDecisionNoteLength })
        {
            return $"Decision note must be {MaxDecisionNoteLength} characters or fewer.";
        }

        return null;
    }

    private sealed record PriorityResult(HostNotificationPriority Priority, string Reason);
    private sealed record SafetyResult(bool IsEmergency, string Classification, string Reason);
}
