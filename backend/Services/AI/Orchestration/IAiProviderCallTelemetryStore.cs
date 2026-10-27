using StayFlow.Api.DTOs.AIProvider;

namespace StayFlow.Api.Services.AI.Orchestration;

public interface IAiProviderCallTelemetryStore
{
    Task PrepareAsync(AiProviderCallPreparation preparation, CancellationToken cancellationToken);
    Task<bool> TryStartDispatchAsync(ProviderCallId providerCallId, Guid companyId, DateTimeOffset dispatchStartedAt, CancellationToken cancellationToken);
    Task RecordAsync(AiProviderCallObservation observation, CancellationToken cancellationToken);
    Task RecordIndeterminateAsync(AiProviderCallIndeterminateObservation observation, CancellationToken cancellationToken);
}

public sealed record AiProviderCallPreparation(
    ProviderCallId ProviderCallId,
    Guid CompanyId,
    OuterOperationType? OuterOperationType,
    Guid? OuterOperationId,
    string Provider,
    string? RequestedModel);

public sealed record AiProviderCallObservation(
    ProviderCallId ProviderCallId,
    Guid CompanyId,
    OuterOperationType? OuterOperationType,
    Guid? OuterOperationId,
    string? Provider,
    string? RequestedModel,
    AiTokenUsage? TokenUsage,
    AIProviderOutcome Outcome,
    string? ResolvedModel = null,
    string? ProviderRequestId = null,
    string? FailureCategory = null,
    AiProviderCallLifecycleState LifecycleState = AiProviderCallLifecycleState.Observed,
    DateTimeOffset? CompletedAt = null);

public sealed record AiProviderCallIndeterminateObservation(
    ProviderCallId ProviderCallId,
    Guid CompanyId,
    string FailureCategory,
    DateTimeOffset CompletedAt);

public sealed class AiProviderCallTelemetryConflictException(string message) : InvalidOperationException(message);

public sealed class AiProviderCallPersistenceException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);
