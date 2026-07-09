using System.Diagnostics;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIProvider;

namespace StayFlow.Api.Services;

public sealed class OpenAIAIProvider(
    IOpenAIResponsesClient responsesClient,
    IOptions<OpenAIOptions> options,
    ILogger<OpenAIAIProvider> logger) : IAIProvider
{
    private const string ProviderName = "OpenAI";

    public async Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var openAIOptions = options.Value;

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(openAIOptions.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var providerResponse = await responsesClient.CreateResponseAsync(new OpenAIProviderRequest
            {
                Model = openAIOptions.Model ?? string.Empty,
                MaxOutputTokens = openAIOptions.MaxOutputTokens,
                Messages = request.RenderedMessages.Select(MapMessage).ToList()
            }, linkedCts.Token);

            stopwatch.Stop();

            if (providerResponse.IsIncomplete)
            {
                return Failure(AIProviderOutcome.Failed, OpenAIProviderFailureCategories.UnexpectedResponse, openAIOptions.Model, providerResponse.RequestId, stopwatch.ElapsedMilliseconds);
            }

            if (providerResponse.IsRefusal)
            {
                return Failure(AIProviderOutcome.Failed, OpenAIProviderFailureCategories.UnexpectedResponse, openAIOptions.Model, providerResponse.RequestId, stopwatch.ElapsedMilliseconds);
            }

            if (string.IsNullOrWhiteSpace(providerResponse.ResponseText))
            {
                return Failure(AIProviderOutcome.Failed, OpenAIProviderFailureCategories.EmptyResponse, openAIOptions.Model, providerResponse.RequestId, stopwatch.ElapsedMilliseconds);
            }

            logger.LogInformation(
                "OpenAI provider completed. CorrelationId={CorrelationId} Model={Model} DurationMs={DurationMs}",
                request.CorrelationId,
                openAIOptions.Model,
                stopwatch.ElapsedMilliseconds);

            return AIProviderResult.Success(
                providerResponse.ResponseText,
                ProviderName,
                providerResponse.ModelName ?? openAIOptions.Model,
                providerResponse.RequestId,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return Failure(AIProviderOutcome.Unavailable, OpenAIProviderFailureCategories.Cancelled, openAIOptions.Model, null, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return Failure(AIProviderOutcome.Unavailable, OpenAIProviderFailureCategories.Timeout, openAIOptions.Model, null, stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException exception)
        {
            stopwatch.Stop();
            logger.LogWarning(exception, "OpenAI provider timed out. CorrelationId={CorrelationId} Model={Model}", request.CorrelationId, openAIOptions.Model);
            return Failure(AIProviderOutcome.Unavailable, OpenAIProviderFailureCategories.Timeout, openAIOptions.Model, null, stopwatch.ElapsedMilliseconds);
        }
        catch (OpenAIProviderException exception)
        {
            stopwatch.Stop();
            var outcome = FailureCategoryIsUnavailable(exception.FailureCategory) ? AIProviderOutcome.Unavailable : AIProviderOutcome.Failed;
            logger.LogWarning(
                exception,
                "OpenAI provider failed. CorrelationId={CorrelationId} Model={Model} FailureCategory={FailureCategory}",
                request.CorrelationId,
                openAIOptions.Model,
                exception.FailureCategory);
            return Failure(outcome, exception.FailureCategory, openAIOptions.Model, null, stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException exception)
        {
            stopwatch.Stop();
            logger.LogWarning(exception, "OpenAI provider network failure. CorrelationId={CorrelationId} Model={Model}", request.CorrelationId, openAIOptions.Model);
            return Failure(AIProviderOutcome.Unavailable, OpenAIProviderFailureCategories.Network, openAIOptions.Model, null, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogError(exception, "OpenAI provider failed unexpectedly. CorrelationId={CorrelationId} Model={Model}", request.CorrelationId, openAIOptions.Model);
            return Failure(AIProviderOutcome.Failed, OpenAIProviderFailureCategories.Unknown, openAIOptions.Model, null, stopwatch.ElapsedMilliseconds);
        }
    }

    private static OpenAIProviderMessage MapMessage(AIPromptMessage message)
    {
        return new OpenAIProviderMessage
        {
            Role = NormalizeRole(message.Role),
            Content = message.Content
        };
    }

    private static string NormalizeRole(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "system" => "system",
            "developer" => "developer",
            "assistant" => "assistant",
            _ => "user"
        };
    }

    private static bool FailureCategoryIsUnavailable(string failureCategory)
    {
        return failureCategory is OpenAIProviderFailureCategories.Timeout
            or OpenAIProviderFailureCategories.RateLimited
            or OpenAIProviderFailureCategories.ProviderServerError
            or OpenAIProviderFailureCategories.Network
            or OpenAIProviderFailureCategories.Cancelled;
    }

    private static AIProviderResult Failure(AIProviderOutcome outcome, string failureCategory, string? modelName, string? requestId, long durationMs)
    {
        return new AIProviderResult
        {
            Outcome = outcome,
            ProviderName = ProviderName,
            ModelName = modelName,
            RequestId = requestId,
            DurationMs = durationMs,
            FailureCategory = failureCategory
        };
    }
}
