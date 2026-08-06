using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Copilot;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Services;

public sealed class CopilotService(
    IConversationContextBuilder conversationContextBuilder,
    IContextConfidenceEvaluator confidenceEvaluator,
    ICurrentTenantContext currentTenantContext,
    IAIReplyOrchestrator replyOrchestrator,
    ISubscriptionEntitlementService? subscriptionEntitlementService = null) : ICopilotService
{
    private readonly ISubscriptionEntitlementService _subscriptionEntitlementService = subscriptionEntitlementService ?? NoOpSubscriptionEntitlementService.Instance;

    public async Task<ApiResponse<ConversationCopilotSummaryResponse>> GetSummaryAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ConversationCopilotSummaryResponse>.Fail(tenantError, [tenantError]);
        }

        await _subscriptionEntitlementService.EnsureFeatureEnabledAsync(companyId, FeatureKeys.HostCopilot, cancellationToken);

        var context = await conversationContextBuilder.BuildAsync(companyId, conversationId, cancellationToken);
        if (context is null)
        {
            return ApiResponse<ConversationCopilotSummaryResponse>.Fail("Conversation was not found.");
        }

        var confidence = confidenceEvaluator.Evaluate(context);
        var latestGuestMessage = context.VisibleMessages.LastOrDefault(message => string.Equals(message.SenderType, "Guest", StringComparison.OrdinalIgnoreCase));
        var summary = BuildDeterministicSummary(context, latestGuestMessage?.Text);

        return ApiResponse<ConversationCopilotSummaryResponse>.Ok(new ConversationCopilotSummaryResponse
        {
            ConversationId = conversationId,
            Summary = summary,
            LatestGuestMessage = latestGuestMessage?.Text,
            VisibleMessageCount = context.VisibleMessages.Count,
            Confidence = MapConfidence(confidence),
            Sources = context.Sources.Select(MapSource).ToList(),
            Warnings = context.Warnings.Select(MapWarning).ToList(),
            ContextTruncated = context.Truncated,
            GeneratedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<ApiResponse<ConversationCopilotSuggestionsResponse>> GetSuggestedRepliesAsync(
        Guid conversationId,
        string? tone,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ConversationCopilotSuggestionsResponse>.Fail(tenantError, [tenantError]);
        }

        await _subscriptionEntitlementService.EnsureFeatureEnabledAsync(companyId, FeatureKeys.HostCopilot, cancellationToken);
        await _subscriptionEntitlementService.ConsumeQuotaAsync(
            companyId,
            UsageMetric.AiRequests,
            1,
            $"copilot:suggestions:{conversationId:D}:{currentTenantContext.CorrelationId ?? "none"}",
            cancellationToken);

        var result = await replyOrchestrator.OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
        {
            ConversationId = conversationId,
            Operation = AIReplyOperation.SuggestedHostReplies,
            RequestedTone = tone,
            RequestedSuggestionCount = 3,
            CorrelationId = currentTenantContext.CorrelationId
        }, cancellationToken);

        if (result is null)
        {
            return ApiResponse<ConversationCopilotSuggestionsResponse>.Fail("Conversation was not found.");
        }

        return ApiResponse<ConversationCopilotSuggestionsResponse>.Ok(new ConversationCopilotSuggestionsResponse
        {
            ConversationId = conversationId,
            SuggestedReplies = result.Suggestions.Take(3).ToList(),
            ContextMessageCount = result.ContextMessageCount,
            DetectedIntent = result.DetectedIntent?.Intent.ToString(),
            Confidence = MapConfidence(result.Confidence, result.FallbackUsed),
            Sources = result.Sources.Select(MapSource).ToList(),
            Warnings = result.Warnings.Select(warning => warning.Code).Distinct(StringComparer.Ordinal).ToList(),
            OrchestrationWarnings = result.Warnings.Select(MapOrchestrationWarning).ToList(),
            Provider = result.Provider,
            IsMock = result.IsMock,
            FallbackUsed = result.FallbackUsed,
            ContextTruncated = result.ContextTruncated,
            GeneratedAt = result.GeneratedAt
        });
    }

    public async Task<ApiResponse<CopilotSuggestReplyResponse>> SuggestHostReplyAsync(
        Guid conversationId,
        CopilotSuggestReplyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<CopilotSuggestReplyResponse>.Fail(tenantError, [tenantError]);
        }

        await _subscriptionEntitlementService.EnsureFeatureEnabledAsync(companyId, FeatureKeys.HostCopilot, cancellationToken);
        await _subscriptionEntitlementService.ConsumeQuotaAsync(
            companyId,
            UsageMetric.AiRequests,
            1,
            $"copilot:draft:{conversationId:D}:{currentTenantContext.CorrelationId ?? "none"}",
            cancellationToken);

        var result = await replyOrchestrator.OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
        {
            ConversationId = conversationId,
            Operation = AIReplyOperation.GeneratedHostReply,
            RequestedTone = request.Tone,
            HostDraft = request.HostDraft,
            HostInstruction = request.Guidance,
            CorrelationId = currentTenantContext.CorrelationId
        }, cancellationToken);

        if (result is null)
        {
            return ApiResponse<CopilotSuggestReplyResponse>.Fail("Conversation was not found.");
        }

        return ApiResponse<CopilotSuggestReplyResponse>.Ok(new CopilotSuggestReplyResponse
        {
            ConversationId = conversationId,
            SuggestedReply = result.Output ?? string.Empty,
            Tone = request.Tone,
            DetectedIntent = result.DetectedIntent?.Intent.ToString(),
            Rationale = result.FallbackUsed
                ? "Generated safe fallback draft because orchestration safeguards required fallback handling."
                : "Generated from conversation context, detected intent, and approved knowledge.",
            ContextMessageCount = result.ContextMessageCount,
            IsFallback = result.FallbackUsed,
            FallbackUsed = result.FallbackUsed,
            RequiresHumanReview = result.RequiresHumanReview,
            Provider = result.Provider,
            IsMock = result.IsMock,
            ProviderMetadata = new CopilotProviderMetadataResponse
            {
                ProviderName = result.Provider,
                ModelName = result.IsMock ? "deterministic" : null,
                RequestId = null
            },
            Confidence = MapConfidence(result.Confidence, result.FallbackUsed),
            Sources = result.Sources.Select(MapSource).ToList(),
            Warnings = result.Warnings.Select(warning => warning.Code).Distinct(StringComparer.Ordinal).ToList(),
            OrchestrationWarnings = result.Warnings.Select(MapOrchestrationWarning).ToList(),
            ContextTruncated = result.ContextTruncated,
            GeneratedAt = result.GeneratedAt
        });
    }

    public async Task<ApiResponse<ConversationRetrievalDiagnosticsResponse>> GetRetrievalDiagnosticsAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ConversationRetrievalDiagnosticsResponse>.Fail(tenantError, [tenantError]);
        }

        await _subscriptionEntitlementService.EnsureFeatureEnabledAsync(companyId, FeatureKeys.HostCopilot, cancellationToken);

        var result = await replyOrchestrator.OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
        {
            ConversationId = conversationId,
            Operation = AIReplyOperation.SuggestedHostReplies,
            RequestedSuggestionCount = 3,
            CorrelationId = currentTenantContext.CorrelationId
        }, cancellationToken);

        if (result is null)
        {
            return ApiResponse<ConversationRetrievalDiagnosticsResponse>.Fail("Conversation was not found.");
        }

        var diagnostics = result.RetrievalDiagnostics ?? new RetrievalDiagnosticsSnapshot
        {
            DetectedIntent = result.DetectedIntent?.Intent.ToString() ?? "Unknown",
            IntentAmbiguous = result.DetectedIntent?.Ambiguous ?? false,
            IntentConfidenceScore = (int)Math.Round((result.DetectedIntent?.ConfidenceScore ?? 0) * 100),
            SecondaryIntentCount = 0,
            CandidateCount = 0,
            SelectedCount = 0,
            ConfidenceLevel = Services.AI.Retrieval.KnowledgeConfidenceLevel.None,
            ReasonCode = Services.AI.Retrieval.KnowledgeRetrievalReasonCode.NoMatch,
            ClarificationRequired = false,
            EscalationRecommended = result.RequiresHumanReview,
            SelectedCategories = [],
            ClarificationChoices = [],
            WarningCodes = result.Warnings.Select(item => item.Code).Distinct(StringComparer.Ordinal).ToArray(),
            EvaluationMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
        };

        return ApiResponse<ConversationRetrievalDiagnosticsResponse>.Ok(new ConversationRetrievalDiagnosticsResponse
        {
            ConversationId = conversationId,
            Diagnostics = MapRetrievalDiagnostics(diagnostics),
            ContextTruncated = result.ContextTruncated,
            FallbackUsed = result.FallbackUsed,
            RequiresHumanReview = result.RequiresHumanReview,
            Provider = result.Provider,
            DurationMilliseconds = result.DurationMilliseconds,
            GeneratedAt = result.GeneratedAt
        });
    }

    private bool TryGetCompanyId(out Guid companyId, out string error)
    {
        companyId = currentTenantContext.CompanyId ?? Guid.Empty;
        if (!currentTenantContext.IsAuthenticated || companyId == Guid.Empty)
        {
            error = "Authenticated tenant context is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static CopilotConfidenceDto MapConfidence(ContextConfidenceResult confidence)
    {
        return new CopilotConfidenceDto
        {
            Score = confidence.Score,
            Level = confidence.Level.ToString(),
            Reasons = confidence.Reasons,
            MissingContext = confidence.MissingContext.Select(item => item.ToString()).ToList()
        };
    }

    private static CopilotConfidenceDto MapConfidence(int confidenceScore, bool fallbackUsed)
    {
        var level = confidenceScore >= 80
            ? "High"
            : confidenceScore >= 50
                ? "Medium"
                : "Low";

        var reasons = fallbackUsed
            ? new[] { "Safe fallback handling was applied due to provider or validation constraints." }
            : new[] { "Confidence was evaluated from available conversation and approved context coverage." };

        return new CopilotConfidenceDto
        {
            Score = confidenceScore,
            Level = level,
            Reasons = reasons,
            MissingContext = []
        };
    }

    private static CopilotSourceDto MapSource(ConversationContextSource source)
    {
        return new CopilotSourceDto
        {
            SourceType = source.SourceType.ToString(),
            Title = source.Title,
            Category = source.Category,
            RelevanceReason = source.RelevanceReason,
            LastUpdated = source.LastUpdated
        };
    }

    private static CopilotOrchestrationWarningDto MapOrchestrationWarning(AIReplyOrchestrationWarning warning)
    {
        return new CopilotOrchestrationWarningDto
        {
            Code = warning.Code,
            Message = warning.Message,
            Severity = warning.Severity
        };
    }

    private static CopilotRetrievalDiagnosticsDto MapRetrievalDiagnostics(RetrievalDiagnosticsSnapshot snapshot)
    {
        return new CopilotRetrievalDiagnosticsDto
        {
            DetectedIntent = snapshot.DetectedIntent,
            IntentAmbiguous = snapshot.IntentAmbiguous,
            IntentConfidenceScore = snapshot.IntentConfidenceScore,
            SecondaryIntentCount = snapshot.SecondaryIntentCount,
            CandidateCount = snapshot.CandidateCount,
            SelectedCount = snapshot.SelectedCount,
            ConfidenceLevel = snapshot.ConfidenceLevel.ToString(),
            ReasonCode = snapshot.ReasonCode.ToString(),
            ClarificationRequired = snapshot.ClarificationRequired,
            EscalationRecommended = snapshot.EscalationRecommended,
            SelectedCategories = snapshot.SelectedCategories,
            ClarificationChoices = snapshot.ClarificationChoices,
            WarningCodes = snapshot.WarningCodes,
            EvaluationMetadata = snapshot.EvaluationMetadata
        };
    }

    private static string MapWarning(ConversationContextWarning warning) => warning.ToString();

    private static string BuildDeterministicSummary(
        ConversationContext context,
        string? latestGuestMessage)
    {
        var propertyName = string.IsNullOrWhiteSpace(context.PropertyName)
            ? "the property"
            : context.PropertyName;
        var latestSnippet = string.IsNullOrWhiteSpace(latestGuestMessage)
            ? "No guest message yet"
            : Truncate(latestGuestMessage.Trim(), 120);

        return $"{context.GuestDisplayName} conversation at {propertyName} is currently {context.Status}. Visible messages: {context.VisibleMessages.Count}. Latest guest message: {latestSnippet}.";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..Math.Max(0, maxLength - 3)].TrimEnd()}...";
    }
}
