using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Services;

public sealed class AccountingAIProvider(
    IAIProvider innerProvider,
    string providerName,
    string? requestedModel,
    IAiProviderCallTelemetryStore telemetryStore,
    TimeProvider timeProvider,
    ILogger<AccountingAIProvider> logger) : IAIProvider
{
    public async Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CompanyId == Guid.Empty)
        {
            throw new ArgumentException("CompanyId is required for provider-call accounting.", nameof(request));
        }

        var preparation = new AiProviderCallPreparation(
            request.ProviderCallId,
            request.CompanyId,
            request.OuterOperationType,
            request.OuterOperationId,
            providerName,
            requestedModel);

        try
        {
            await telemetryStore.PrepareAsync(preparation, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "AI provider call preparation failed. ProviderCallId={ProviderCallId} CompanyId={CompanyId}",
                request.ProviderCallId.Value,
                request.CompanyId);
            throw new AiProviderCallPersistenceException(
                $"Could not prepare provider-call accounting for '{request.ProviderCallId.Value}'.",
                exception);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var dispatchStartedAt = timeProvider.GetUtcNow();
        bool dispatchClaimed;
        try
        {
            dispatchClaimed = await telemetryStore.TryStartDispatchAsync(
                request.ProviderCallId,
                request.CompanyId,
                dispatchStartedAt,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "AI provider dispatch claim failed. ProviderCallId={ProviderCallId} CompanyId={CompanyId}",
                request.ProviderCallId.Value,
                request.CompanyId);
            throw new AiProviderCallPersistenceException(
                $"Could not claim provider-call dispatch for '{request.ProviderCallId.Value}'.",
                exception);
        }

        if (!dispatchClaimed)
        {
            throw new AiProviderCallTelemetryConflictException(
                $"Provider call '{request.ProviderCallId.Value}' is not eligible for dispatch.");
        }

        AIProviderResult result;
        try
        {
            result = await innerProvider.GenerateAsync(request, cancellationToken);
        }
        catch (Exception exception)
        {
            var failureCategory = GetFailureCategory(exception);
            try
            {
                await telemetryStore.RecordIndeterminateAsync(
                    new AiProviderCallIndeterminateObservation(
                        request.ProviderCallId,
                        request.CompanyId,
                        failureCategory,
                        timeProvider.GetUtcNow()),
                    CancellationToken.None);
            }
            catch (Exception persistenceException)
            {
                logger.LogError(persistenceException,
                    "AI provider indeterminate observation persistence failed. ProviderCallId={ProviderCallId} CompanyId={CompanyId}",
                    request.ProviderCallId.Value,
                    request.CompanyId);
                throw new AiProviderCallPersistenceException(
                    $"Could not persist the indeterminate provider-call observation for '{request.ProviderCallId.Value}'.",
                    persistenceException);
            }

            throw;
        }

        var lifecycleState = IsIndeterminate(result.FailureCategory)
            ? AiProviderCallLifecycleState.Indeterminate
            : AiProviderCallLifecycleState.Observed;
        try
        {
            await telemetryStore.RecordAsync(
                new AiProviderCallObservation(
                    request.ProviderCallId,
                    request.CompanyId,
                    request.OuterOperationType,
                    request.OuterOperationId,
                    providerName,
                    requestedModel,
                    result.TokenUsage,
                    result.Outcome,
                    result.ResolvedModel,
                    result.RequestId,
                    result.FailureCategory,
                    lifecycleState,
                    timeProvider.GetUtcNow()),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "AI provider completion persistence failed. ProviderCallId={ProviderCallId} CompanyId={CompanyId}",
                request.ProviderCallId.Value,
                request.CompanyId);
            throw new AiProviderCallPersistenceException(
                $"Could not persist the provider-call observation for '{request.ProviderCallId.Value}'.",
                exception);
        }

        return new AIProviderResult
        {
            Outcome = result.Outcome,
            ResponseText = result.ResponseText,
            ProviderName = providerName,
            RequestedModel = requestedModel,
            ResolvedModel = result.ResolvedModel,
            ModelName = result.ResolvedModel ?? result.ModelName ?? requestedModel,
            RequestId = result.RequestId,
            DurationMs = result.DurationMs,
            TokenUsage = result.TokenUsage,
            FailureCategory = result.FailureCategory,
            ProviderCallId = request.ProviderCallId,
            OuterOperationType = request.OuterOperationType,
            OuterOperationId = request.OuterOperationId
        };
    }

    private static bool IsIndeterminate(string? failureCategory)
    {
        return failureCategory is OpenAIProviderFailureCategories.Timeout
            or OpenAIProviderFailureCategories.Cancelled
            or OpenAIProviderFailureCategories.Network;
    }

    private static string GetFailureCategory(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => OpenAIProviderFailureCategories.Cancelled,
            TimeoutException => OpenAIProviderFailureCategories.Timeout,
            HttpRequestException => OpenAIProviderFailureCategories.Network,
            OpenAIProviderException providerException => providerException.FailureCategory,
            _ => OpenAIProviderFailureCategories.Unknown
        };
    }
}
