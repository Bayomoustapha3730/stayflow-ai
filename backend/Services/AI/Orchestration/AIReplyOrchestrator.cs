using System.Diagnostics;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Grounding;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Memory;
using StayFlow.Api.Services.AI.Retrieval;
using StayFlow.Api.Services.AI.Safety;
using StayFlow.Api.Services.AI.Validation;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class AIReplyOrchestrator(
    IConversationContextBuilder conversationContextBuilder,
    IGuestIntentDetector intentDetector,
    IPropertyKnowledgeRanker knowledgeRanker,
    IAIPromptBuilder promptBuilder,
    IAIProvider aiProvider,
    IAIReplyOutputValidator outputValidator,
    IAIReplySafetyEvaluator safetyEvaluator,
    IContextConfidenceEvaluator confidenceEvaluator,
    IAIReplyFallbackProvider fallbackProvider,
    IOptions<AIReplyOrchestratorOptions> options,
    ILogger<AIReplyOrchestrator> logger,
    IConversationIntentRecognizer? conversationIntentRecognizer = null,
    IConversationMemoryService? conversationMemoryService = null,
    IPropertyKnowledgeRetriever? propertyKnowledgeRetriever = null,
    IConciergeResponseGenerator? conciergeResponseGenerator = null,
    IOptions<ConciergeIntelligenceOptions>? conciergeOptions = null) : IAIReplyOrchestrator
{
    public async Task<AIReplyOrchestrationResult?> OrchestrateAsync(
        Guid companyId,
        AIReplyOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var completedStages = new List<AIReplyOrchestrationStage>();
        var warnings = new List<AIReplyOrchestrationWarning>();
        var orchestratorOptions = options.Value;
        AIProviderResult? providerResult = null;
        var fallbackUsed = false;

        ValidateRequest(request);
        completedStages.Add(AIReplyOrchestrationStage.RequestValidated);

        var context = await conversationContextBuilder.BuildAsync(companyId, request.ConversationId, cancellationToken);
        if (context is null)
        {
            return null;
        }

        logger.LogInformation(
            "AI reply trace: context built. ConversationId={ConversationId} CompanyId={CompanyId} PropertyId={PropertyId} ReservationId={ReservationId} ApprovedKnowledgeCount={ApprovedKnowledgeCount} HumanTakeoverEnabled={HumanTakeoverEnabled} RequiresHostAttention={RequiresHostAttention} WarningCount={WarningCount}",
            request.ConversationId,
            context.TenantId,
            context.PropertyId,
            context.ReservationId,
            context.ApprovedKnowledgeItems.Count,
            context.HumanTakeoverEnabled,
            context.RequiresHostAttention,
            context.Warnings.Count);

        completedStages.Add(AIReplyOrchestrationStage.ContextLoaded);

        warnings.AddRange(context.Warnings.Select(item => new AIReplyOrchestrationWarning(
            "ContextWarning",
            item.ToString(),
            "info")));

        var latestGuestMessage = context.VisibleMessages
            .LastOrDefault(message => string.Equals(message.SenderType, "Guest", StringComparison.OrdinalIgnoreCase))
            ?.Text ?? string.Empty;

        var intelligence = conciergeOptions?.Value ?? new ConciergeIntelligenceOptions();
        var v2IntentRecognizer = conversationIntentRecognizer ?? new ConversationIntentRecognizer();
        var memoryService = conversationMemoryService ?? new ConversationMemoryService(v2IntentRecognizer);
        var memory = memoryService.BuildContext(
            context,
            intelligence.RecentMessageCount,
            intelligence.MemoryCharacterBudget);

        var intentResult = v2IntentRecognizer.Recognize(
            latestGuestMessage,
            memory.ActiveTopic is null ? null : [memory.ActiveTopic],
            intelligence.MaximumIntents);
        var intent = intentResult.ToGuestIntentResult();
        completedStages.Add(AIReplyOrchestrationStage.IntentDetected);

        var ranking = propertyKnowledgeRetriever is null
            ? knowledgeRanker.Rank(
                context,
                intent,
                latestGuestMessage,
                Math.Clamp(orchestratorOptions.MaximumSelectedKnowledgeItems, 1, 8),
                Math.Max(500, orchestratorOptions.MaximumSelectedKnowledgeCharacters))
            : propertyKnowledgeRetriever.Retrieve(
                context,
                new KnowledgeRetrievalRequest(
                    companyId,
                    context.PropertyId,
                    request.ConversationId,
                    latestGuestMessage,
                    intentResult,
                    memory,
                    intelligence.MaximumCandidates,
                    intelligence.MaximumSelectedItems,
                    intelligence.ContextCharacterBudget));

        completedStages.Add(AIReplyOrchestrationStage.KnowledgeRanked);

        foreach (var reason in ranking.Reasons)
        {
            warnings.Add(new AIReplyOrchestrationWarning("KnowledgeRanking", reason, "info"));
        }

        logger.LogInformation(
            "AI reply trace: ranking completed. SelectedCount={SelectedCount} RankedCandidateCount={RankedCandidateCount} RejectedCount={RejectedCount} Ambiguous={Ambiguous}",
            ranking.SelectedItems.Count,
            ranking.Candidates.Count,
            Math.Max(0, ranking.Candidates.Count - ranking.SelectedItems.Count),
            ranking.RequiresClarification);

        logger.LogInformation(
            "Knowledge retrieval completed. ConversationId={ConversationId} PropertyId={PropertyId} Intent={Intent} SecondaryIntentCount={SecondaryIntentCount} CandidateCount={CandidateCount} SelectedCount={SelectedCount} ConfidenceLevel={ConfidenceLevel} TopCategory={TopCategory} ReasonCode={ReasonCode}",
            request.ConversationId,
            context.PropertyId,
            intent.Intent,
            intentResult.SecondaryIntents.Count,
            ranking.Candidates.Count,
            ranking.SelectedItems.Count,
            ranking.ConfidenceLevel,
            ranking.SelectedItems.FirstOrDefault()?.Category.ToString() ?? "None",
            ranking.ReasonCode);

        var contextConfidence = confidenceEvaluator.Evaluate(context);
        completedStages.Add(AIReplyOrchestrationStage.ConfidenceEvaluated);

        var expectedSuggestions = Math.Clamp(request.RequestedSuggestionCount ?? 3, 1, 3);
        if (request.Operation == AIReplyOperation.SuggestedHostReplies && expectedSuggestions != 3)
        {
            warnings.Add(new AIReplyOrchestrationWarning("SuggestionCountNormalized", "Suggestion count was normalized to the supported deterministic range.", "info"));
        }

        var generatedAt = DateTimeOffset.UtcNow;
        var output = string.Empty;
        var suggestions = new List<string>();
        var conflictRequiresReview = false;

        var operationMaxChars = request.Operation == AIReplyOperation.GeneratedHostReply ? 1500 : 700;

        var selectedKnowledge = ranking.SelectedItems
            .Select(candidate => MapProviderKnowledge(candidate.Item))
            .ToList();

        if (conciergeResponseGenerator is not null
            && request.Operation != AIReplyOperation.SuggestedHostReplies
            && !string.IsNullOrWhiteSpace(latestGuestMessage))
        {
            var generated = conciergeResponseGenerator.Generate(new ConciergeResponseRequest(
                latestGuestMessage,
                intentResult,
                ranking,
                memory,
                context.PropertyName,
                context.ConfirmationNumber,
                ParseTone(request.RequestedTone, intelligence.DefaultTone),
                "en",
                context.HumanTakeoverEnabled));

            output = generated.Text;
            conflictRequiresReview = generated.RequiresEscalation;
            if (generated.RequiresClarification)
            {
                warnings.Add(new AIReplyOrchestrationWarning("Clarification", "Clarification was requested by concierge response generator.", "info"));
            }
        }

        logger.LogInformation(
            "AI reply trace: orchestrator input. DetectedIntent={DetectedIntent} IntentConfidence={IntentConfidence} SelectedKnowledgeCount={SelectedKnowledgeCount}",
            intent.Intent,
            intent.ConfidenceScore,
            selectedKnowledge.Count);

        if (intent.Intent == GuestIntent.WiFi && selectedKnowledge.Count > 0)
        {
            var wifiGrounding = DeterministicGrounding.ExtractWiFi(selectedKnowledge);
            if (wifiGrounding.HasConflict)
            {
                conflictRequiresReview = true;
                warnings.Add(new AIReplyOrchestrationWarning(
                    "ConflictingApprovedKnowledge",
                    "Conflicting approved Wi-Fi information was found.",
                    "warning"));

                var conflictMessage = "Conflicting approved Wi-Fi information was found. Please verify the network details with the host before sending a reply.";
                if (request.Operation == AIReplyOperation.SuggestedHostReplies)
                {
                    suggestions = BuildSuggestions(conflictMessage, request.RequestedTone, expectedSuggestions);
                }
                else
                {
                    output = conflictMessage;
                }
            }
        }

        var ambiguousAccessClarification = intentResult.IsAmbiguous && IsAccessClarificationRequest(latestGuestMessage);

        if (!conflictRequiresReview
            && ((ranking.RequiresClarification && ShouldAskClarification(intentResult)) || ambiguousAccessClarification)
            && request.Operation != AIReplyOperation.SuggestedHostReplies
            && intent.Intent != GuestIntent.Emergency
            && intent.Intent != GuestIntent.PetPolicy)
        {
            output = BuildClarificationPrompt(ranking.ClarificationChoices, latestGuestMessage);
        }

        if (!conflictRequiresReview
            && string.IsNullOrWhiteSpace(output)
            && ranking.SelectedItems.Count == 0
            && intent.Intent != GuestIntent.Emergency
            && request.Operation != AIReplyOperation.SuggestedHostReplies)
        {
            output = BuildKnowledgeNotFoundResponse(intent.Intent, latestGuestMessage);
        }

        if (!conflictRequiresReview
            && string.IsNullOrWhiteSpace(output)
            && (!string.IsNullOrWhiteSpace(latestGuestMessage) || request.Operation == AIReplyOperation.FutureGuestReply))
        {
            var prompt = promptBuilder.BuildReply(new AIReplyPromptBuildRequest
            {
                ConversationContext = context,
                Intent = intent,
                SelectedKnowledgeItems = ranking.SelectedItems.Select(candidate => candidate.Item).ToList(),
                RetrievalConfidenceLevel = ranking.ConfidenceLevel,
                RetrievalReasonCode = ranking.ReasonCode.ToString(),
                Operation = request.Operation,
                RequestedTone = request.RequestedTone,
                HostInstruction = request.HostInstruction,
                HostDraft = request.HostDraft,
                MaxResponseCharacters = operationMaxChars
            });

            completedStages.Add(AIReplyOrchestrationStage.PromptBuilt);

            logger.LogInformation(
                "AI reply trace: prompt built. SelectedKnowledgeCount={SelectedKnowledgeCount} PromptMessageCount={PromptMessageCount}",
                ranking.SelectedItems.Count,
                prompt.RenderedMessages.Count);

            providerResult = await InvokeProviderWithTimeoutAsync(
                prompt,
                selectedKnowledge,
                intent.Intent.ToString(),
                request.RequestedTone,
                request.CorrelationId,
                orchestratorOptions.ProviderTimeoutSeconds,
                cancellationToken);
            completedStages.Add(AIReplyOrchestrationStage.ProviderInvoked);

            if (providerResult.Outcome == AIProviderOutcome.Success && !string.IsNullOrWhiteSpace(providerResult.ResponseText))
            {
                if (request.Operation == AIReplyOperation.SuggestedHostReplies)
                {
                    suggestions = BuildSuggestions(providerResult.ResponseText!, request.RequestedTone, expectedSuggestions);
                }
                else
                {
                    output = providerResult.ResponseText!;
                }
            }

            logger.LogInformation(
                "AI reply trace: provider response. Outcome={Outcome} Provider={Provider} FailureCategory={FailureCategory}",
                providerResult.Outcome,
                providerResult.ProviderName,
                providerResult.FailureCategory);
        }

        var validation = outputValidator.Validate(
            request.Operation,
            output,
            suggestions,
            operationMaxChars,
            expectedSuggestions,
            contextConfidence.Level != ContextConfidenceLevel.High || context.Truncated);

        completedStages.Add(AIReplyOrchestrationStage.OutputValidated);

        logger.LogInformation(
            "AI reply trace: validator output. IsValid={IsValid} ErrorCount={ErrorCount} WarningCount={WarningCount}",
            validation.IsValid,
            validation.Errors.Count,
            validation.Warnings.Count);

        warnings.AddRange(validation.Warnings);

        if (!validation.IsValid)
        {
            warnings.AddRange(validation.Errors.Select(error => new AIReplyOrchestrationWarning("Validation", error)));
        }

        output = validation.NormalizedOutput ?? string.Empty;
        suggestions = validation.NormalizedSuggestions.ToList();
        completedStages.Add(AIReplyOrchestrationStage.OutputNormalized);

        var safety = safetyEvaluator.Evaluate(
            request.Operation,
            output,
            suggestions,
            context,
            ranking.SelectedItems.Select(candidate => candidate.Item).ToList(),
            intent,
            contextConfidence.Score,
            fallbackUsed);

        completedStages.Add(AIReplyOrchestrationStage.SafetyEvaluated);
        warnings.AddRange(safety.Warnings);

        var providerFailed = conflictRequiresReview
            ? false
            : !string.IsNullOrWhiteSpace(output)
                ? false
            : providerResult is null
            || providerResult.Outcome != AIProviderOutcome.Success
            || (request.Operation == AIReplyOperation.SuggestedHostReplies ? suggestions.Count == 0 : string.IsNullOrWhiteSpace(output));
        var validationFailed = !validation.IsValid;
        var safetyBlocked = safety.BlockedReasons.Count > 0;
        var contextInsufficient = conflictRequiresReview
            ? false
            : string.IsNullOrWhiteSpace(latestGuestMessage)
                || (string.IsNullOrWhiteSpace(output) && ranking.SelectedItems.Count == 0 && !ranking.RequiresClarification);

        if (request.Operation == AIReplyOperation.FutureGuestReply)
        {
            warnings.Add(new AIReplyOrchestrationWarning(
                "FutureGuestReplyNotEnabled",
                "Future guest replies are not enabled for autonomous dispatch.",
                "info"));
        }

        if (orchestratorOptions.EnableFallback && (providerFailed || validationFailed || safetyBlocked || contextInsufficient))
        {
            fallbackUsed = true;
            completedStages.Add(AIReplyOrchestrationStage.FallbackApplied);

            var fallbackText = fallbackProvider.BuildFallback(
                request.Operation,
                request.RequestedTone,
                intent,
                includeReviewReminder: true);

            if (request.Operation == AIReplyOperation.SuggestedHostReplies)
            {
                suggestions = BuildSuggestions(fallbackText, request.RequestedTone, 3);
            }
            else
            {
                output = fallbackText;
            }

            warnings.Add(new AIReplyOrchestrationWarning(
                "FallbackUsed",
                "Safe fallback content was used due to limited or unsafe generation conditions."));
        }

        var safetyAfterFallback = safetyEvaluator.Evaluate(
            request.Operation,
            output,
            suggestions,
            context,
            ranking.SelectedItems.Select(candidate => candidate.Item).ToList(),
            intent,
            contextConfidence.Score,
            fallbackUsed);

        warnings.AddRange(safetyAfterFallback.Warnings);

        var sourceIds = new HashSet<string>(ranking.SelectedItems.Select(item => item.ArticleId), StringComparer.OrdinalIgnoreCase);
        var sources = context.Sources
            .Where(source => source.SourceType != ConversationContextSourceType.PropertyKnowledge || (source.SourceId is not null && sourceIds.Contains(source.SourceId)))
            .ToList();

        completedStages.Add(AIReplyOrchestrationStage.SourcesAssembled);

        var confidence = (int)Math.Round((contextConfidence.Score * 0.45) + (ranking.Confidence * 0.55));
        if (intent.Ambiguous || ranking.RequiresClarification)
        {
            confidence = Math.Max(0, confidence - 10);
        }

        if (fallbackUsed)
        {
            confidence = Math.Min(confidence, 45);
        }

        if (conflictRequiresReview)
        {
            confidence = Math.Min(confidence, 35);
        }

        if (request.Operation == AIReplyOperation.FutureGuestReply)
        {
            confidence = Math.Min(confidence, 40);
        }

        completedStages.Add(AIReplyOrchestrationStage.ResultAssembled);
        stopwatch.Stop();

        logger.LogInformation(
            "AI reply orchestration completed. Operation={Operation} ConversationId={ConversationId} Provider={Provider} DurationMs={DurationMs} FallbackUsed={FallbackUsed} SourceCount={SourceCount} WarningCount={WarningCount} RequiresHumanReview={RequiresHumanReview}",
            request.Operation,
            request.ConversationId,
            providerResult?.ProviderName ?? "fallback",
            stopwatch.ElapsedMilliseconds,
            fallbackUsed,
            sources.Count,
            warnings.Count,
            conflictRequiresReview
                || safetyAfterFallback.RequiresHumanReview
                || request.Operation == AIReplyOperation.FutureGuestReply);

        return new AIReplyOrchestrationResult
        {
            ConversationId = request.ConversationId,
            Operation = request.Operation,
            Output = request.Operation == AIReplyOperation.SuggestedHostReplies ? null : output,
            Suggestions = request.Operation == AIReplyOperation.SuggestedHostReplies ? suggestions : [],
            ContextMessageCount = context.VisibleMessages.Count,
            Confidence = confidence,
            Sources = sources,
            Warnings = warnings.Distinct().ToArray(),
            DetectedIntent = intent,
            Provider = providerResult?.ProviderName ?? "Fallback",
            IsMock = string.Equals(providerResult?.ProviderName, "Development", StringComparison.OrdinalIgnoreCase),
            GeneratedAt = generatedAt,
            ContextTruncated = context.Truncated,
            FallbackUsed = fallbackUsed,
            CompletedStages = completedStages,
            DurationMilliseconds = stopwatch.ElapsedMilliseconds,
            RequiresHumanReview = conflictRequiresReview
                || safetyAfterFallback.RequiresHumanReview
                || request.Operation == AIReplyOperation.FutureGuestReply,
            RetrievalDiagnostics = new RetrievalDiagnosticsSnapshot
            {
                DetectedIntent = intent.Intent.ToString(),
                IntentAmbiguous = intentResult.IsAmbiguous,
                IntentConfidenceScore = (int)Math.Round(intentResult.Confidence * 100),
                SecondaryIntentCount = intentResult.SecondaryIntents.Count,
                CandidateCount = ranking.Candidates.Count,
                SelectedCount = ranking.SelectedItems.Count,
                ConfidenceLevel = ranking.ConfidenceLevel,
                ReasonCode = ranking.ReasonCode,
                ClarificationRequired = ranking.RequiresClarification,
                EscalationRecommended = ranking.EscalationRecommended,
                SelectedCategories = ranking.SelectedItems
                    .Select(item => item.Category.ToString())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                ClarificationChoices = ranking.ClarificationChoices,
                WarningCodes = warnings
                    .Select(item => item.Code)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                EvaluationMetadata = ranking.EvaluationMetadata
            }
        };
    }

    private static ConciergeTone ParseTone(string? requestedTone, string defaultTone)
    {
        var tone = string.IsNullOrWhiteSpace(requestedTone) ? defaultTone : requestedTone;
        return tone.Trim().ToLowerInvariant() switch
        {
            "professional" => ConciergeTone.Professional,
            "concise" => ConciergeTone.Concise,
            _ => ConciergeTone.Warm
        };
    }

    private static string BuildClarificationPrompt(IReadOnlyCollection<string> clarificationChoices, string latestGuestMessage)
    {
        if (IsAccessClarificationRequest(latestGuestMessage))
        {
            return "Are you asking about check-in time, property entry, or Wi-Fi access?";
        }

        var choices = clarificationChoices
            .Select(MapGuestFacingClarificationChoice)
            .Where(choice => !string.IsNullOrWhiteSpace(choice))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        if (choices.Length == 0)
        {
            return "Could you clarify what you need help with so I can provide the right details?";
        }

        if (choices.Length == 1)
        {
            return $"Could you clarify whether you are asking about {choices[0]}?";
        }

        if (choices.Length == 2)
        {
            return $"Are you asking about {choices[0]} or {choices[1]}?";
        }

        return $"Are you asking about {choices[0]}, {choices[1]}, or {choices[2]}?";
    }

    private static bool ShouldAskClarification(ConversationIntentResult intentResult)
    {
        if (intentResult.AllIntents().Count > 1 && !intentResult.IsAmbiguous)
        {
            return false;
        }

        return intentResult.IsAmbiguous || intentResult.Confidence < 0.70;
    }

    private static bool IsAccessClarificationRequest(string latestGuestMessage)
    {
        var normalized = latestGuestMessage.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Contains("access", StringComparison.Ordinal)
            || normalized.Contains("enter", StringComparison.Ordinal)
            || normalized.Contains("entry", StringComparison.Ordinal)
            || normalized.Contains("get in", StringComparison.Ordinal)
            || normalized.Contains("getting in", StringComparison.Ordinal)
            || normalized.Contains("inside", StringComparison.Ordinal)
            || normalized.Contains("unlock", StringComparison.Ordinal)
            || normalized.Contains("building", StringComparison.Ordinal);
    }

    private static string BuildKnowledgeNotFoundResponse(GuestIntent intent, string latestGuestMessage)
    {
        if (intent == GuestIntent.PetPolicy)
        {
            return "I couldn't find a pet policy for this property. I can notify the host to confirm whether pets are allowed.";
        }

        var normalized = latestGuestMessage.Trim().ToLowerInvariant();
        if (normalized.Contains("curtain", StringComparison.Ordinal) && normalized.Contains("color", StringComparison.Ordinal))
        {
            return "I don't have information about the curtain color. I can ask the host if you'd like.";
        }

        return "I could not find approved property information for that request. I can help contact the host so they can assist you directly.";
    }

    private static string MapGuestFacingClarificationChoice(string rawChoice)
    {
        var normalized = rawChoice.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (normalized.Contains("check-in", StringComparison.Ordinal)
            || normalized.Contains("checkin", StringComparison.Ordinal)
            || normalized.Contains("arrival", StringComparison.Ordinal))
        {
            return "check-in time";
        }

        if (normalized.Contains("propertyaccess", StringComparison.Ordinal)
            || normalized.Contains("access code", StringComparison.Ordinal)
            || normalized.Contains("door", StringComparison.Ordinal)
            || normalized.Contains("entry", StringComparison.Ordinal)
            || normalized.Contains("property entry", StringComparison.Ordinal))
        {
            return "property entry";
        }

        if (normalized.Contains("wifi", StringComparison.Ordinal)
            || normalized.Contains("wi-fi", StringComparison.Ordinal)
            || normalized.Contains("wireless", StringComparison.Ordinal))
        {
            return "Wi-Fi access";
        }

        if (normalized.Contains("parking", StringComparison.Ordinal))
        {
            return "parking";
        }

        if (normalized.Contains("checkout", StringComparison.Ordinal)
            || normalized.Contains("check-out", StringComparison.Ordinal))
        {
            return "checkout details";
        }

        if (normalized.Contains("pet", StringComparison.Ordinal)
            || normalized.Contains("animal", StringComparison.Ordinal))
        {
            return "pet policy";
        }

        if (normalized.Contains("house", StringComparison.Ordinal)
            || normalized.Contains("rule", StringComparison.Ordinal))
        {
            return "house rules";
        }

        if (normalized.Contains("local", StringComparison.Ordinal)
            || normalized.Contains("restaurant", StringComparison.Ordinal)
            || normalized.Contains("nearby", StringComparison.Ordinal))
        {
            return "local recommendations";
        }

        return "your request";
    }

    private static AIProviderKnowledgeItem MapProviderKnowledge(ConversationContextKnowledgeItem item)
    {
        return new AIProviderKnowledgeItem
        {
            SourceId = item.SourceId,
            Title = item.Title,
            Category = item.Category.ToString(),
            Tags = item.Tags,
            Summary = item.Summary,
            Content = item.Content,
            Priority = item.Priority,
            IsApproved = item.IsApproved
        };
    }

    private static List<string> BuildSuggestions(string baseText, string? tone, int count)
    {
        var normalized = baseText.Trim();
        var direct = normalized;
        var clarification = "Could you share a few more details so I can confirm this accurately before we proceed?";
        var followUp = tone?.Trim().ToLowerInvariant() switch
        {
            "friendly" => "Thanks again for your message. I\'m confirming the details now and will update you shortly.",
            "luxury" => "Thank you for your patience. I\'m verifying the details and will follow up with a precise update shortly.",
            "casual" => "Thanks, I\'m checking this now and will get back to you shortly.",
            _ => "Thank you for your message. I\'m verifying the details and will provide a clear update shortly."
        };

        var all = new[] { direct, clarification, followUp }
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Max(1, count))
            .ToList();

        while (all.Count < count)
        {
            all.Add(followUp);
            all = all.Distinct(StringComparer.Ordinal).ToList();
            if (all.Count < count)
            {
                all.Add($"{followUp} Please verify before sending.");
                all = all.Distinct(StringComparer.Ordinal).ToList();
            }
        }

        return all.Take(count).ToList();
    }

    private async Task<AIProviderResult> InvokeProviderWithTimeoutAsync(
        AIPromptPackage prompt,
        IReadOnlyCollection<AIProviderKnowledgeItem> selectedKnowledge,
        string detectedIntent,
        string? requestedTone,
        string? correlationId,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 3, 60)));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var mappedCategories = MapCategories(detectedIntent);
            logger.LogInformation(
                "AI reply trace: provider request. DetectedIntent={DetectedIntent} Categories={Categories} SelectedKnowledgeCount={SelectedKnowledgeCount} PromptMessageCount={PromptMessageCount}",
                detectedIntent,
                mappedCategories.Select(category => category.ToString()).ToArray(),
                selectedKnowledge.Count,
                prompt.RenderedMessages.Count);

            return await aiProvider.GenerateAsync(new AIProviderRequest
            {
                PromptPackage = prompt,
                RenderedMessages = prompt.RenderedMessages,
                ResponseConstraints = prompt.ResponseConstraints,
                QuestionCategories = mappedCategories,
                SelectedKnowledgeItems = selectedKnowledge,
                DetectedIntent = detectedIntent,
                RequestedTone = requestedTone,
                CorrelationId = correlationId
            }, linked.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new AIProviderResult
            {
                Outcome = AIProviderOutcome.Unavailable,
                ProviderName = "TimedOut",
                FailureCategory = "Timeout"
            };
        }
        catch (Exception)
        {
            return new AIProviderResult
            {
                Outcome = AIProviderOutcome.Failed,
                ProviderName = "Exception",
                FailureCategory = "ProviderException"
            };
        }
    }

    private static void ValidateRequest(AIReplyOrchestrationRequest request)
    {
        if (request.ConversationId == Guid.Empty)
        {
            throw new ArgumentException("ConversationId is required.", nameof(request));
        }
    }

    private static IReadOnlyCollection<QuestionContextCategory> MapCategories(string detectedIntent)
    {
        return detectedIntent.Trim().ToLowerInvariant() switch
        {
            "wifi" => [QuestionContextCategory.WiFi],
            "checkin" or "earlycheckin" or "latearrival" => [QuestionContextCategory.CheckIn],
            "checkout" => [QuestionContextCategory.CheckOut],
            "parking" => [QuestionContextCategory.Parking],
            "houserules" or "noise" or "petpolicy" => [QuestionContextCategory.HouseRules],
            "localrecommendations" => [QuestionContextCategory.Restaurant],
            "amenities" => [QuestionContextCategory.Amenities],
            "access" or "propertyaccess" => [QuestionContextCategory.PropertyAccess],
            "reservation" => [QuestionContextCategory.CheckIn],
            "payment" or "hostcontact" => [QuestionContextCategory.General],
            "generalproperty" => [QuestionContextCategory.General],
            "laundry" => [QuestionContextCategory.Laundry],
            "emergency" or "maintenance" => [QuestionContextCategory.Emergency],
            _ => [QuestionContextCategory.General]
        };
    }
}
