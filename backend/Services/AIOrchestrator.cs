using System.Text.Json;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.DTOs.AIResponseValidation;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class AIOrchestrator(
    IAIContextBuilder aiContextBuilder,
    IAIPromptBuilder aiPromptBuilder,
    IAIProvider aiProvider,
    IAIResponseValidator aiResponseValidator,
    IAIContextRepository aiContextRepository,
    ICurrentTenantContext currentTenantContext,
    ILogger<AIOrchestrator> logger,
    IHostEnvironment hostEnvironment) : IAIOrchestrator
{
    public async Task<AIOrchestrationResult> ProcessAsync(AIOrchestrationRequest request, CancellationToken cancellationToken)
    {
        AIContextBuildResult? contextResult = null;
        AIProviderResult? providerResult = null;
        AIResponseValidationResult? validationResult = null;
        var providerWasInvoked = false;

        try
        {
            logger.LogInformation(
                "AI orchestration request accepted. CorrelationId={CorrelationId} HasGuestId={HasGuestId} HasConversationId={HasConversationId} Channel={Channel}",
                currentTenantContext.CorrelationId,
                request.GuestId.HasValue,
                request.ConversationId.HasValue,
                request.Channel);

            contextResult = await aiContextBuilder.BuildAsync(new AIContextRequest
            {
                GuestQuestion = request.GuestMessage,
                GuestId = request.GuestId,
                ConversationId = request.ConversationId,
                Channel = request.Channel,
                ChannelIdentity = request.ChannelIdentity,
                ExplicitReservationReference = request.ExplicitReservationReference,
                ExplicitPropertyName = request.ExplicitPropertyName,
                CurrentTimestamp = request.CurrentTimestamp
            }, cancellationToken);

            logger.LogInformation(
                "AI orchestration context built. CorrelationId={CorrelationId} Outcome={Outcome} Categories={Categories} CandidateCount={CandidateCount} EscalationReason={EscalationReason}",
                currentTenantContext.CorrelationId,
                contextResult.Outcome,
                contextResult.QuestionCategories.Select(category => category.ToString()).ToArray(),
                contextResult.CandidateLabels.Count,
                contextResult.EscalationReason);

            if (contextResult.Outcome == AIContextBuildOutcome.ClarificationRequired)
            {
                logger.LogInformation(
                    "AI orchestration candidate labels generated. CorrelationId={CorrelationId} CandidateCount={CandidateCount}",
                    currentTenantContext.CorrelationId,
                    contextResult.CandidateLabels.Count);
                var result = new AIOrchestrationResult
                {
                    Outcome = AIOrchestrationOutcome.ClarificationRequired,
                    GuestSafeMessage = AIOrchestrationSafeMessages.ClarificationRequired,
                    CandidateLabels = contextResult.CandidateLabels,
                    QuestionCategories = contextResult.QuestionCategories
                };
                ApplyDevelopmentDiagnostics(result, contextResult, providerWasInvoked);
                await AuditAsync(result, contextResult, providerResult, validationResult, cancellationToken);
                return result;
            }

            if (contextResult.Outcome == AIContextBuildOutcome.EscalationRequired)
            {
                logger.LogWarning(
                    "AI orchestration escalated before provider invocation. CorrelationId={CorrelationId} EscalationReason={EscalationReason}",
                    currentTenantContext.CorrelationId,
                    contextResult.EscalationReason);
                var result = new AIOrchestrationResult
                {
                    Outcome = AIOrchestrationOutcome.EscalationRequired,
                    GuestSafeMessage = AIOrchestrationSafeMessages.HostAssistanceRequired,
                    QuestionCategories = contextResult.QuestionCategories
                };
                ApplyDevelopmentDiagnostics(result, contextResult, providerWasInvoked);
                await AuditAsync(result, contextResult, providerResult, validationResult, cancellationToken);
                return result;
            }

            if (contextResult.Outcome == AIContextBuildOutcome.NoEligibleReservation && !CanUseGeneralContext(contextResult))
            {
                var result = new AIOrchestrationResult
                {
                    Outcome = AIOrchestrationOutcome.NoEligibleReservation,
                    GuestSafeMessage = AIOrchestrationSafeMessages.NoEligibleReservation,
                    QuestionCategories = contextResult.QuestionCategories
                };
                ApplyDevelopmentDiagnostics(result, contextResult, providerWasInvoked);
                await AuditAsync(result, contextResult, providerResult, validationResult, cancellationToken);
                return result;
            }

            if (contextResult.Context is null)
            {
                logger.LogWarning(
                    "AI orchestration escalated because context was missing. CorrelationId={CorrelationId} ContextBuildOutcome={ContextBuildOutcome} EscalationReason={EscalationReason}",
                    currentTenantContext.CorrelationId,
                    contextResult.Outcome,
                    contextResult.EscalationReason);
                var result = new AIOrchestrationResult
                {
                    Outcome = AIOrchestrationOutcome.EscalationRequired,
                    GuestSafeMessage = AIOrchestrationSafeMessages.HostAssistanceRequired,
                    QuestionCategories = contextResult.QuestionCategories
                };
                ApplyDevelopmentDiagnostics(result, contextResult, providerWasInvoked);
                await AuditAsync(result, contextResult, providerResult, validationResult, cancellationToken);
                return result;
            }

            var promptPackage = aiPromptBuilder.Build(new AIPromptBuildRequest
            {
                GuestQuestion = request.GuestMessage,
                AIContext = contextResult.Context,
                QuestionCategories = contextResult.QuestionCategories
            });

            logger.LogInformation(
                "AI provider selected. CorrelationId={CorrelationId} ProviderType={ProviderType}",
                currentTenantContext.CorrelationId,
                aiProvider.GetType().Name);
            logger.LogInformation(
                "AI provider invocation started. CorrelationId={CorrelationId} MessageCount={MessageCount}",
                currentTenantContext.CorrelationId,
                promptPackage.RenderedMessages.Count);
            providerWasInvoked = true;
            providerResult = await aiProvider.GenerateAsync(new AIProviderRequest
            {
                PromptPackage = promptPackage,
                RenderedMessages = promptPackage.RenderedMessages,
                ResponseConstraints = promptPackage.ResponseConstraints,
                QuestionCategories = contextResult.QuestionCategories,
                CorrelationId = currentTenantContext.CorrelationId
            }, cancellationToken);

            logger.LogInformation(
                "AI provider response received. CorrelationId={CorrelationId} Outcome={Outcome} ProviderName={ProviderName} ModelName={ModelName} FailureCategory={FailureCategory}",
                currentTenantContext.CorrelationId,
                providerResult.Outcome,
                providerResult.ProviderName,
                providerResult.ModelName,
                providerResult.FailureCategory);

            if (providerResult.Outcome == AIProviderOutcome.Unavailable)
            {
                var result = ProviderUnavailable(providerResult, contextResult.QuestionCategories);
                ApplyDevelopmentDiagnostics(result, contextResult, providerWasInvoked);
                await AuditAsync(result, contextResult, providerResult, validationResult, cancellationToken);
                return result;
            }

            if (providerResult.Outcome == AIProviderOutcome.Failed)
            {
                var result = ProviderUnavailable(providerResult, contextResult.QuestionCategories);
                ApplyDevelopmentDiagnostics(result, contextResult, providerWasInvoked);
                await AuditAsync(result, contextResult, providerResult, validationResult, cancellationToken);
                return result;
            }

            validationResult = aiResponseValidator.Validate(new AIResponseValidationRequest
            {
                ModelResponse = providerResult.ResponseText,
                AIContext = contextResult.Context,
                QuestionCategories = contextResult.QuestionCategories,
                PromptPackage = promptPackage,
                ProtectedIdentifiers = new AIProtectedIdentifiers
                {
                    CompanyId = contextResult.Metadata.CompanyId,
                    GuestId = contextResult.Metadata.GuestId,
                    ReservationId = contextResult.Metadata.ReservationId,
                    PropertyId = contextResult.Metadata.PropertyId
                }
            });

            logger.LogInformation(
                "AI response validation completed. CorrelationId={CorrelationId} Outcome={Outcome} Violations={Violations}",
                currentTenantContext.CorrelationId,
                validationResult.Outcome,
                validationResult.Violations.Select(violation => violation.ToString()).ToArray());

            var mapped = MapValidationResult(providerResult, validationResult, contextResult.QuestionCategories);
            ApplyDevelopmentDiagnostics(mapped, contextResult, providerWasInvoked);
            await AuditAsync(mapped, contextResult, providerResult, validationResult, cancellationToken);
            return mapped;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "AI orchestration failed unexpectedly. CorrelationId={CorrelationId} ContextBuildOutcome={ContextBuildOutcome} ProviderOutcome={ProviderOutcome}",
                currentTenantContext.CorrelationId,
                contextResult?.Outcome,
                providerResult?.Outcome);
            var result = new AIOrchestrationResult
            {
                Outcome = AIOrchestrationOutcome.ProviderUnavailable,
                GuestSafeMessage = AIOrchestrationSafeMessages.ProviderUnavailable,
                QuestionCategories = contextResult?.QuestionCategories ?? []
            };
            ApplyDevelopmentDiagnostics(result, contextResult, providerWasInvoked);
            await AuditAsync(result, contextResult, providerResult, validationResult, cancellationToken);
            return result;
        }
    }

    private static AIOrchestrationResult MapValidationResult(
        AIProviderResult providerResult,
        AIResponseValidationResult validationResult,
        IReadOnlyCollection<QuestionContextCategory> categories)
    {
        return validationResult.Outcome switch
        {
            AIResponseValidationOutcome.Valid => new AIOrchestrationResult
            {
                Outcome = AIOrchestrationOutcome.Responded,
                GuestSafeMessage = providerResult.ResponseText ?? AIOrchestrationSafeMessages.GeneralResponseUnavailable,
                QuestionCategories = categories,
                ProviderMetadata = Metadata(providerResult)
            },
            AIResponseValidationOutcome.Blocked => new AIOrchestrationResult
            {
                Outcome = AIOrchestrationOutcome.Blocked,
                GuestSafeMessage = validationResult.SafeMessage ?? AIResponseSafeMessages.GeneralValidationFailure,
                QuestionCategories = categories,
                ValidationViolations = validationResult.Violations,
                ProviderMetadata = Metadata(providerResult)
            },
            AIResponseValidationOutcome.EscalationRequired => new AIOrchestrationResult
            {
                Outcome = AIOrchestrationOutcome.EscalationRequired,
                GuestSafeMessage = validationResult.SafeMessage ?? AIOrchestrationSafeMessages.HostAssistanceRequired,
                QuestionCategories = categories,
                ValidationViolations = validationResult.Violations,
                ProviderMetadata = Metadata(providerResult)
            },
            _ => new AIOrchestrationResult
            {
                Outcome = AIOrchestrationOutcome.Blocked,
                GuestSafeMessage = AIResponseSafeMessages.GeneralValidationFailure,
                QuestionCategories = categories,
                ProviderMetadata = Metadata(providerResult)
            }
        };
    }

    private static AIOrchestrationResult ProviderUnavailable(AIProviderResult providerResult, IReadOnlyCollection<QuestionContextCategory> categories)
    {
        return new AIOrchestrationResult
        {
            Outcome = AIOrchestrationOutcome.ProviderUnavailable,
            GuestSafeMessage = AIOrchestrationSafeMessages.ProviderUnavailable,
            QuestionCategories = categories,
            ProviderMetadata = Metadata(providerResult)
        };
    }

    private static AIProviderMetadata Metadata(AIProviderResult providerResult)
    {
        return new AIProviderMetadata
        {
            ProviderName = providerResult.ProviderName,
            ModelName = providerResult.ModelName,
            RequestId = providerResult.RequestId,
            DurationMs = providerResult.DurationMs
        };
    }

    private static bool CanUseGeneralContext(AIContextBuildResult contextResult)
    {
        return contextResult.Context is not null
            && !contextResult.Context.Safety.RequiresPropertyAccessAuthorization
            && contextResult.Context.Reservation is null
            && contextResult.Context.Property is null;
    }

    private void ApplyDevelopmentDiagnostics(
        AIOrchestrationResult result,
        AIContextBuildResult? contextResult,
        bool providerWasInvoked)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            return;
        }

        result.ContextBuildOutcome = contextResult?.Outcome.ToString();
        result.EscalationReason = contextResult?.EscalationReason;
        result.ContextBuildMessage = contextResult?.Message;
        result.ReservationContextOutcome = contextResult?.ReservationContextOutcome;
        result.ReservationContextMessage = contextResult?.ReservationContextMessage;
        result.ProviderSelected = aiProvider.GetType().Name;
        result.ProviderWasInvoked = providerWasInvoked;
    }

    private async Task AuditAsync(
        AIOrchestrationResult result,
        AIContextBuildResult? contextResult,
        AIProviderResult? providerResult,
        AIResponseValidationResult? validationResult,
        CancellationToken cancellationToken)
    {
        if (result.Outcome == AIOrchestrationOutcome.Responded)
        {
            return;
        }

        await aiContextRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "AIOrchestration",
            EntityId = Guid.Empty,
            Action = result.Outcome.ToString(),
            Details = JsonSerializer.Serialize(new
            {
                currentTenantContext.CorrelationId,
                OrchestrationOutcome = result.Outcome.ToString(),
                ContextBuildOutcome = contextResult?.Outcome.ToString(),
                EscalationReason = contextResult?.EscalationReason,
                QuestionCategories = result.QuestionCategories.Select(category => category.ToString()).ToList(),
                ProviderName = providerResult?.ProviderName,
                ProviderModel = providerResult?.ModelName,
                ProviderDurationMs = providerResult?.DurationMs,
                ValidationOutcome = validationResult?.Outcome.ToString(),
                ValidationViolationCodes = validationResult?.Violations.Select(violation => violation.ToString()).ToList() ?? []
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await aiContextRepository.SaveChangesAsync(cancellationToken);
    }
}
