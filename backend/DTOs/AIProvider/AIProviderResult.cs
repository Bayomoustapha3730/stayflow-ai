namespace StayFlow.Api.DTOs.AIProvider;

public sealed class AIProviderResult
{
    public AIProviderOutcome Outcome { get; init; }
    public string? ResponseText { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string? RequestedModel { get; init; }
    public string? ResolvedModel { get; init; }
    public string? ModelName { get; init; }
    public string? RequestId { get; init; }
    public long DurationMs { get; init; }
    public AiTokenUsage? TokenUsage { get; init; }
    public string? FailureCategory { get; init; }
    public ProviderCallId? ProviderCallId { get; init; }
    public OuterOperationType? OuterOperationType { get; init; }
    public Guid? OuterOperationId { get; init; }

    public static AIProviderResult Success(
        string responseText,
        string providerName,
        string? modelName,
        string? requestId,
        long durationMs,
        AiTokenUsage? tokenUsage = null,
        ProviderCallId? providerCallId = null,
        OuterOperationType? outerOperationType = null,
        Guid? outerOperationId = null,
        string? requestedModel = null)
    {
        return new AIProviderResult
        {
            Outcome = AIProviderOutcome.Success,
            ResponseText = responseText,
            ProviderName = providerName,
            RequestedModel = requestedModel,
            ResolvedModel = modelName,
            ModelName = modelName ?? requestedModel,
            RequestId = requestId,
            DurationMs = durationMs,
            TokenUsage = tokenUsage,
            ProviderCallId = providerCallId,
            OuterOperationType = outerOperationType,
            OuterOperationId = outerOperationId
        };
    }
}
