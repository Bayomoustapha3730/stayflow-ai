using System.Diagnostics;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
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
    ILogger<AIReplyOrchestrator> logger) : IAIReplyOrchestrator
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

        completedStages.Add(AIReplyOrchestrationStage.ContextLoaded);

        warnings.AddRange(context.Warnings.Select(item => new AIReplyOrchestrationWarning(
            "ContextWarning",
            item.ToString(),
            "info")));

        var intent = intentDetector.Detect(context);
        completedStages.Add(AIReplyOrchestrationStage.IntentDetected);

        var latestGuestMessage = context.VisibleMessages
            .LastOrDefault(message => string.Equals(message.SenderType, "Guest", StringComparison.OrdinalIgnoreCase))
            ?.Text ?? string.Empty;

        var ranking = knowledgeRanker.Rank(
            context,
            intent,
            latestGuestMessage,
            Math.Clamp(orchestratorOptions.MaximumSelectedKnowledgeItems, 1, 8),
            Math.Max(500, orchestratorOptions.MaximumSelectedKnowledgeCharacters));

        completedStages.Add(AIReplyOrchestrationStage.KnowledgeRanked);

        foreach (var reason in ranking.Reasons)
        {
            warnings.Add(new AIReplyOrchestrationWarning("KnowledgeRanking", reason, "info"));
        }

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

        var operationMaxChars = request.Operation == AIReplyOperation.GeneratedHostReply ? 1500 : 700;

        if (!string.IsNullOrWhiteSpace(latestGuestMessage) || request.Operation == AIReplyOperation.FutureGuestReply)
        {
            var prompt = promptBuilder.BuildReply(new AIReplyPromptBuildRequest
            {
                ConversationContext = context,
                Intent = intent,
                SelectedKnowledgeItems = ranking.SelectedItems,
                Operation = request.Operation,
                RequestedTone = request.RequestedTone,
                HostInstruction = request.HostInstruction,
                HostDraft = request.HostDraft,
                MaxResponseCharacters = operationMaxChars
            });

            completedStages.Add(AIReplyOrchestrationStage.PromptBuilt);

            providerResult = await InvokeProviderWithTimeoutAsync(prompt, request.CorrelationId, orchestratorOptions.ProviderTimeoutSeconds, cancellationToken);
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
        }

        var validation = outputValidator.Validate(
            request.Operation,
            output,
            suggestions,
            operationMaxChars,
            expectedSuggestions,
            contextConfidence.Level != ContextConfidenceLevel.High || context.Truncated);

        completedStages.Add(AIReplyOrchestrationStage.OutputValidated);

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
            contextConfidence.Score,
            fallbackUsed);

        completedStages.Add(AIReplyOrchestrationStage.SafetyEvaluated);
        warnings.AddRange(safety.Warnings);

        var providerFailed = providerResult is null
            || providerResult.Outcome != AIProviderOutcome.Success
            || (request.Operation == AIReplyOperation.SuggestedHostReplies ? suggestions.Count == 0 : string.IsNullOrWhiteSpace(output));
        var validationFailed = !validation.IsValid;
        var safetyBlocked = safety.BlockedReasons.Count > 0;
        var contextInsufficient = string.IsNullOrWhiteSpace(latestGuestMessage) || ranking.SelectedItems.Count == 0;

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
            contextConfidence.Score,
            fallbackUsed);

        warnings.AddRange(safetyAfterFallback.Warnings);

        var sourceTitles = new HashSet<string>(ranking.SelectedItems.Select(item => item.Title), StringComparer.OrdinalIgnoreCase);
        var sources = context.Sources
            .Where(source => source.SourceType != ConversationContextSourceType.PropertyKnowledge || sourceTitles.Contains(source.Title))
            .ToList();

        completedStages.Add(AIReplyOrchestrationStage.SourcesAssembled);

        var confidence = contextConfidence.Score;
        if (intent.Ambiguous)
        {
            confidence = Math.Max(0, confidence - 8);
        }

        if (fallbackUsed)
        {
            confidence = Math.Min(confidence, 45);
        }

        if (request.Operation == AIReplyOperation.FutureGuestReply)
        {
            confidence = Math.Min(confidence, 40);
        }

        completedStages.Add(AIReplyOrchestrationStage.ResultAssembled);
        stopwatch.Stop();

        logger.LogInformation(
            "AI reply orchestration completed. Operation={Operation} ConversationId={ConversationId} Provider={Provider} DurationMs={DurationMs} FallbackUsed={FallbackUsed} SourceCount={SourceCount} WarningCount={WarningCount}",
            request.Operation,
            request.ConversationId,
            providerResult?.ProviderName ?? "fallback",
            stopwatch.ElapsedMilliseconds,
            fallbackUsed,
            sources.Count,
            warnings.Count);

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
            RequiresHumanReview = safetyAfterFallback.RequiresHumanReview || request.Operation == AIReplyOperation.FutureGuestReply
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
        string? correlationId,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 3, 60)));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            return await aiProvider.GenerateAsync(new AIProviderRequest
            {
                PromptPackage = prompt,
                RenderedMessages = prompt.RenderedMessages,
                ResponseConstraints = prompt.ResponseConstraints,
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
}
