namespace StayFlow.Api.DTOs.AIProvider;

public sealed class AIProviderResult
{
    public AIProviderOutcome Outcome { get; init; }
    public string? ResponseText { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string? ModelName { get; init; }
    public string? RequestId { get; init; }
    public long DurationMs { get; init; }
    public AiTokenUsage? TokenUsage { get; init; }
    public string? FailureCategory { get; init; }

    public static AIProviderResult Success(
        string responseText,
        string providerName,
        string? modelName,
        string? requestId,
        long durationMs,
        AiTokenUsage? tokenUsage = null)
    {
        return new AIProviderResult
        {
            Outcome = AIProviderOutcome.Success,
            ResponseText = responseText,
            ProviderName = providerName,
            ModelName = modelName,
            RequestId = requestId,
            DurationMs = durationMs,
            TokenUsage = tokenUsage
        };
    }
}
