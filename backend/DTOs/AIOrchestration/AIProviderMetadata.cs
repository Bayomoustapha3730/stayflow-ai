namespace StayFlow.Api.DTOs.AIOrchestration;

public sealed class AIProviderMetadata
{
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public string? RequestId { get; init; }
    public string? ProviderCallId { get; init; }
    public string? OuterOperationType { get; init; }
    public Guid? OuterOperationId { get; init; }
    public long? DurationMs { get; init; }
}
