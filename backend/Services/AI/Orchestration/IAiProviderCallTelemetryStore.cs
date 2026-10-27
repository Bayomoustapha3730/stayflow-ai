using StayFlow.Api.DTOs.AIProvider;

namespace StayFlow.Api.Services.AI.Orchestration;

public interface IAiProviderCallTelemetryStore
{
    Task RecordAsync(AiProviderCallObservation observation, CancellationToken cancellationToken);
}

public sealed record AiProviderCallObservation(
    ProviderCallId ProviderCallId,
    Guid CompanyId,
    OuterOperationType? OuterOperationType,
    Guid? OuterOperationId,
    string? Provider,
    string? Model,
    AiTokenUsage? TokenUsage,
    AIProviderOutcome Outcome);

public sealed class AiProviderCallTelemetryConflictException(string message) : InvalidOperationException(message);
