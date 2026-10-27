using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.Data.Configurations;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class AiProviderCallTelemetryStore(
    DbContextOptions<ApplicationDbContext> dbContextOptions,
    ITenantContext? tenantContext = null) : IAiProviderCallTelemetryStore
{
    private const int MaximumAttempts = 3;

    public async Task PrepareAsync(AiProviderCallPreparation preparation, CancellationToken cancellationToken)
    {
        ValidatePreparation(preparation);

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            await using var dbContext = new ApplicationDbContext(dbContextOptions, tenantContext);
            IDbContextTransaction? transaction = null;
            try
            {
                if (dbContext.Database.IsRelational())
                {
                    transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                }

                await ValidateOuterOwnershipAsync(dbContext, preparation, cancellationToken);

                var existing = await dbContext.AiProviderCallTelemetries
                    .SingleOrDefaultAsync(call => call.ProviderCallId == preparation.ProviderCallId.Value, cancellationToken);
                if (existing is null)
                {
                    await dbContext.AiProviderCallTelemetries.AddAsync(CreatePreparedTelemetry(preparation), cancellationToken);
                }
                else
                {
                    ValidatePreparationReplay(existing, preparation);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return;
            }
            catch (DbUpdateException exception) when (IsProviderCallIdUniqueViolation(exception))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                if (attempt == MaximumAttempts)
                {
                    await ReconcilePreparationRaceAsync(preparation, exception, cancellationToken);
                    return;
                }
            }
            catch (Exception exception) when (attempt < MaximumAttempts && IsRetryableConcurrencyFailure(exception))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }
    }

    public async Task<bool> TryStartDispatchAsync(
        ProviderCallId providerCallId,
        Guid companyId,
        DateTimeOffset dispatchStartedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerCallId);
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("CompanyId is required.", nameof(companyId));
        }

        await using var dbContext = new ApplicationDbContext(dbContextOptions, tenantContext);
        if (dbContext.Database.IsRelational())
        {
            var updated = await dbContext.AiProviderCallTelemetries
                .Where(call => call.ProviderCallId == providerCallId.Value
                    && call.CompanyId == companyId
                    && call.LifecycleState == AiProviderCallLifecycleState.Prepared)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(call => call.LifecycleState, AiProviderCallLifecycleState.DispatchStarted)
                    .SetProperty(call => call.DispatchStartedAt, dispatchStartedAt)
                    .SetProperty(call => call.UpdatedAt, dispatchStartedAt), cancellationToken);
            return updated == 1;
        }

        var call = await dbContext.AiProviderCallTelemetries
            .SingleOrDefaultAsync(item => item.ProviderCallId == providerCallId.Value && item.CompanyId == companyId, cancellationToken);
        if (call is null || call.LifecycleState != AiProviderCallLifecycleState.Prepared)
        {
            return false;
        }

        call.LifecycleState = AiProviderCallLifecycleState.DispatchStarted;
        call.DispatchStartedAt = dispatchStartedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RecordAsync(AiProviderCallObservation observation, CancellationToken cancellationToken)
    {
        ValidateObservation(observation);
        await using var dbContext = new ApplicationDbContext(dbContextOptions, tenantContext);

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            IDbContextTransaction? transaction = null;
            try
            {
                if (dbContext.Database.IsRelational())
                {
                    transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                }

                await ValidateOuterOwnershipAsync(dbContext, observation, cancellationToken);

                var existing = await dbContext.AiProviderCallTelemetries
                    .SingleOrDefaultAsync(call => call.ProviderCallId == observation.ProviderCallId.Value, cancellationToken);

                if (existing is null)
                {
                    await dbContext.AiProviderCallTelemetries.AddAsync(CreateTelemetry(observation), cancellationToken);
                }
                else
                {
                    MergeObservation(existing, observation);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return;
            }
            catch (DbUpdateException exception) when (IsProviderCallIdUniqueViolation(exception))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                DetachTrackedCall(dbContext, observation.ProviderCallId.Value);
                if (attempt == MaximumAttempts)
                {
                    if (transaction is not null)
                    {
                        await transaction.DisposeAsync();
                        transaction = null;
                    }

                    await ReconcileUniqueRaceAsync(observation, exception, cancellationToken);
                    return;
                }
            }
            catch (Exception exception) when (attempt < MaximumAttempts && IsRetryableConcurrencyFailure(exception))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                DetachTrackedCall(dbContext, observation.ProviderCallId.Value);
            }
            catch
            {
                DetachTrackedCall(dbContext, observation.ProviderCallId.Value);
                throw;
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }
    }

    public async Task RecordIndeterminateAsync(
        AiProviderCallIndeterminateObservation observation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(observation.ProviderCallId);
        if (observation.CompanyId == Guid.Empty)
        {
            throw new ArgumentException("CompanyId is required.", nameof(observation));
        }

        ValidateMetadataLength(observation.FailureCategory, 80, nameof(observation.FailureCategory));
        if (string.IsNullOrWhiteSpace(observation.FailureCategory))
        {
            throw new ArgumentException("A failure category is required for an indeterminate call.", nameof(observation));
        }

        await using var dbContext = new ApplicationDbContext(dbContextOptions, tenantContext);
        var call = await dbContext.AiProviderCallTelemetries
            .SingleOrDefaultAsync(item => item.ProviderCallId == observation.ProviderCallId.Value, cancellationToken);
        if (call is null || call.CompanyId != observation.CompanyId)
        {
            throw new AiProviderCallTelemetryConflictException(
                $"Provider call '{observation.ProviderCallId.Value}' has no matching prepared tenant record.");
        }

        if (call.LifecycleState == AiProviderCallLifecycleState.Indeterminate
            && string.Equals(call.FailureCategory, observation.FailureCategory, StringComparison.Ordinal))
        {
            return;
        }

        if (call.LifecycleState != AiProviderCallLifecycleState.DispatchStarted)
        {
            throw new AiProviderCallTelemetryConflictException(
                $"Provider call '{observation.ProviderCallId.Value}' cannot become indeterminate from '{call.LifecycleState}'.");
        }

        call.LifecycleState = AiProviderCallLifecycleState.Indeterminate;
        call.FailureCategory = observation.FailureCategory;
        call.CompletedAt = observation.CompletedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ReconcileUniqueRaceAsync(
        AiProviderCallObservation observation,
        DbUpdateException uniqueViolation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            await using var dbContext = new ApplicationDbContext(dbContextOptions, tenantContext);
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;

            try
            {
                await ValidateOuterOwnershipAsync(dbContext, observation, cancellationToken);

                var existing = await dbContext.AiProviderCallTelemetries
                    .SingleOrDefaultAsync(call => call.ProviderCallId == observation.ProviderCallId.Value, cancellationToken);
                if (existing is null)
                {
                    throw uniqueViolation;
                }

                MergeObservation(existing, observation);
                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return;
            }
            catch (Exception exception) when (attempt < MaximumAttempts && IsRetryableConcurrencyFailure(exception))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not reconcile provider-call telemetry for '{observation.ProviderCallId.Value}' after concurrent updates.");
    }

    private static async Task ValidateOuterOwnershipAsync(
        ApplicationDbContext dbContext,
        AiProviderCallObservation observation,
        CancellationToken cancellationToken)
    {
        if (observation.OuterOperationType is null)
        {
            return;
        }

        var outerId = observation.OuterOperationId!.Value;
        var belongsToCompany = observation.OuterOperationType.Value switch
        {
            OuterOperationType.GuestMessage => await dbContext.ConversationMessages
                .IgnoreQueryFilters()
                .AnyAsync(message => message.Id == outerId && message.CompanyId == observation.CompanyId, cancellationToken),
            OuterOperationType.CopilotOperation => await dbContext.CopilotOperations
                .AnyAsync(operation => operation.Id == outerId && operation.CompanyId == observation.CompanyId, cancellationToken),
            _ => false
        };

        if (!belongsToCompany)
        {
            throw new InvalidOperationException("Provider-call telemetry outer operation does not belong to the specified company.");
        }
    }

    private static AiProviderCallTelemetry CreateTelemetry(AiProviderCallObservation observation)
    {
        return new AiProviderCallTelemetry
        {
            ProviderCallId = observation.ProviderCallId.Value,
            CompanyId = observation.CompanyId,
            OuterOperationType = observation.OuterOperationType,
            OuterOperationId = observation.OuterOperationId,
            Provider = NormalizeProvider(observation.Provider),
            RequestedModel = NormalizeMetadata(observation.RequestedModel),
            ResolvedModel = NormalizeMetadata(observation.ResolvedModel),
            ProviderRequestId = NormalizeMetadata(observation.ProviderRequestId),
            FailureCategory = NormalizeMetadata(observation.FailureCategory),
            LifecycleState = observation.LifecycleState,
            CompletedAt = observation.CompletedAt ?? DateTimeOffset.UtcNow,
            UsageKnown = observation.TokenUsage is not null,
            InputTokens = observation.TokenUsage?.InputTokens,
            OutputTokens = observation.TokenUsage?.OutputTokens,
            TotalTokens = observation.TokenUsage?.TotalTokens,
            Outcome = observation.Outcome
        };
    }

    private static void MergeObservation(
        AiProviderCallTelemetry existing,
        AiProviderCallObservation observation)
    {
        if (existing.CompanyId != observation.CompanyId
            || existing.OuterOperationType != observation.OuterOperationType
            || existing.OuterOperationId != observation.OuterOperationId)
        {
            throw new AiProviderCallTelemetryConflictException(
                $"Provider call '{observation.ProviderCallId.Value}' was observed with conflicting tenant or outer-operation identity.");
        }

        var provider = MergeMetadata(existing.Provider, NormalizeProvider(observation.Provider), observation.ProviderCallId, "provider");
        var requestedModel = MergeMetadata(existing.RequestedModel, observation.RequestedModel, observation.ProviderCallId, "requested model");
        var resolvedModel = MergeMetadata(existing.ResolvedModel, observation.ResolvedModel, observation.ProviderCallId, "resolved model");
        var providerRequestId = MergeMetadata(existing.ProviderRequestId, observation.ProviderRequestId, observation.ProviderCallId, "provider request id");
        var failureCategory = MergeMetadata(existing.FailureCategory, observation.FailureCategory, observation.ProviderCallId, "failure category");

        if (existing.Outcome.HasValue && existing.Outcome != observation.Outcome)
        {
            throw new AiProviderCallTelemetryConflictException(
                $"Provider call '{observation.ProviderCallId.Value}' was observed with conflicting outcomes.");
        }

        if (observation.TokenUsage is { } usage
            && existing.UsageKnown
            && (existing.InputTokens != usage.InputTokens
                || existing.OutputTokens != usage.OutputTokens
                || existing.TotalTokens != usage.TotalTokens))
        {
            throw new AiProviderCallTelemetryConflictException(
                $"Conflicting token usage was recorded for provider call '{observation.ProviderCallId.Value}'.");
        }

        existing.Provider = provider;
        existing.RequestedModel = requestedModel;
        existing.ResolvedModel = resolvedModel;
        existing.ProviderRequestId = providerRequestId;
        existing.FailureCategory = failureCategory;

        if (existing.LifecycleState == AiProviderCallLifecycleState.Observed
            && observation.LifecycleState != AiProviderCallLifecycleState.Observed)
        {
            throw new AiProviderCallTelemetryConflictException(
                $"Provider call '{observation.ProviderCallId.Value}' has conflicting terminal lifecycle states.");
        }

        if (existing.LifecycleState == AiProviderCallLifecycleState.Indeterminate
            && observation.LifecycleState == AiProviderCallLifecycleState.Observed)
        {
            existing.LifecycleState = AiProviderCallLifecycleState.Observed;
        }
        else if (existing.LifecycleState is AiProviderCallLifecycleState.Prepared or AiProviderCallLifecycleState.DispatchStarted)
        {
            existing.LifecycleState = observation.LifecycleState;
        }

        existing.Outcome ??= observation.Outcome;
        existing.CompletedAt ??= observation.CompletedAt ?? DateTimeOffset.UtcNow;

        if (observation.TokenUsage is { } knownUsage && !existing.UsageKnown)
        {
            existing.UsageKnown = true;
            existing.InputTokens = knownUsage.InputTokens;
            existing.OutputTokens = knownUsage.OutputTokens;
            existing.TotalTokens = knownUsage.TotalTokens;
        }
    }

    private static AiProviderCallTelemetry CreatePreparedTelemetry(AiProviderCallPreparation preparation)
    {
        return new AiProviderCallTelemetry
        {
            ProviderCallId = preparation.ProviderCallId.Value,
            CompanyId = preparation.CompanyId,
            OuterOperationType = preparation.OuterOperationType,
            OuterOperationId = preparation.OuterOperationId,
            Provider = NormalizeProvider(preparation.Provider),
            RequestedModel = NormalizeMetadata(preparation.RequestedModel),
            LifecycleState = AiProviderCallLifecycleState.Prepared,
            UsageKnown = false
        };
    }

    private async Task ReconcilePreparationRaceAsync(
        AiProviderCallPreparation preparation,
        DbUpdateException uniqueViolation,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new ApplicationDbContext(dbContextOptions, tenantContext);
        var existing = await dbContext.AiProviderCallTelemetries
            .SingleOrDefaultAsync(call => call.ProviderCallId == preparation.ProviderCallId.Value, cancellationToken);
        if (existing is null)
        {
            throw uniqueViolation;
        }

        ValidatePreparationReplay(existing, preparation);
    }

    private async Task ValidateOuterOwnershipAsync(
        ApplicationDbContext dbContext,
        AiProviderCallPreparation preparation,
        CancellationToken cancellationToken)
    {
        if (preparation.OuterOperationType is null)
        {
            return;
        }

        var belongsToCompany = preparation.OuterOperationType.Value switch
        {
            OuterOperationType.GuestMessage => await dbContext.ConversationMessages
                .IgnoreQueryFilters()
                .AnyAsync(message => message.Id == preparation.OuterOperationId && message.CompanyId == preparation.CompanyId, cancellationToken),
            OuterOperationType.CopilotOperation => await dbContext.CopilotOperations
                .AnyAsync(operation => operation.Id == preparation.OuterOperationId && operation.CompanyId == preparation.CompanyId, cancellationToken),
            _ => false
        };

        if (!belongsToCompany)
        {
            throw new InvalidOperationException("Provider-call telemetry outer operation does not belong to the specified company.");
        }
    }

    private static void ValidatePreparationReplay(AiProviderCallTelemetry existing, AiProviderCallPreparation preparation)
    {
        if (existing.CompanyId != preparation.CompanyId
            || existing.OuterOperationType != preparation.OuterOperationType
            || existing.OuterOperationId != preparation.OuterOperationId
            || !string.Equals(existing.Provider, NormalizeProvider(preparation.Provider), StringComparison.Ordinal)
            || !string.Equals(existing.RequestedModel, NormalizeMetadata(preparation.RequestedModel), StringComparison.Ordinal))
        {
            throw new AiProviderCallTelemetryConflictException(
                $"Provider call '{preparation.ProviderCallId.Value}' was prepared with conflicting tenant, operation, provider, or requested-model identity.");
        }
    }

    private static void ValidatePreparation(AiProviderCallPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(preparation.ProviderCallId);
        if (preparation.ProviderCallId.Value.Length > 64)
        {
            throw new ArgumentException("ProviderCallId exceeds the supported storage length.", nameof(preparation));
        }

        if (preparation.CompanyId == Guid.Empty)
        {
            throw new ArgumentException("CompanyId is required.", nameof(preparation));
        }

        if (preparation.OuterOperationType.HasValue != preparation.OuterOperationId.HasValue
            || preparation.OuterOperationId == Guid.Empty)
        {
            throw new ArgumentException("Outer operation type and ID must be supplied together.", nameof(preparation));
        }

        if (preparation.OuterOperationType is { } type && !Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(preparation), "Outer operation type is not supported.");
        }

        if (string.IsNullOrWhiteSpace(preparation.Provider))
        {
            throw new ArgumentException("Provider is required.", nameof(preparation));
        }

        ValidateMetadataLength(preparation.Provider, 80, nameof(preparation.Provider));
        ValidateMetadataLength(preparation.RequestedModel, 120, nameof(preparation.RequestedModel));
    }

    private static string? MergeMetadata(
        string? existing,
        string? incoming,
        ProviderCallId providerCallId,
        string field)
    {
        incoming = NormalizeMetadata(incoming);
        if (existing is null)
        {
            return incoming;
        }
        if (incoming is not null && !string.Equals(existing, incoming, StringComparison.Ordinal))
        {
            throw new AiProviderCallTelemetryConflictException(
                $"Provider call '{providerCallId.Value}' was observed with conflicting {field} metadata.");
        }

        return existing;
    }

    private static void ValidateObservation(AiProviderCallObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(observation.ProviderCallId);

        if (observation.ProviderCallId.Value.Length > 64)
        {
            throw new ArgumentException("ProviderCallId exceeds the supported storage length.", nameof(observation));
        }

        if (observation.CompanyId == Guid.Empty)
        {
            throw new ArgumentException("CompanyId is required.", nameof(observation));
        }

        if (observation.OuterOperationType.HasValue != observation.OuterOperationId.HasValue
            || observation.OuterOperationId == Guid.Empty)
        {
            throw new ArgumentException("Outer operation type and ID must be supplied together.", nameof(observation));
        }

        if (observation.OuterOperationType is { } type && !Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(observation), "Outer operation type is not supported.");
        }

        if (!Enum.IsDefined(observation.Outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(observation), "Provider outcome is not supported.");
        }

        if (observation.TokenUsage is { } usage
            && (usage.InputTokens < 0 || usage.OutputTokens < 0 || usage.TotalTokens < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(observation), "Provider token usage cannot be negative.");
        }

        ValidateMetadataLength(observation.Provider, 80, nameof(observation.Provider));
        ValidateMetadataLength(observation.RequestedModel, 120, nameof(observation.RequestedModel));
        ValidateMetadataLength(observation.ResolvedModel, 120, nameof(observation.ResolvedModel));
        ValidateMetadataLength(observation.ProviderRequestId, 160, nameof(observation.ProviderRequestId));
        ValidateMetadataLength(observation.FailureCategory, 80, nameof(observation.FailureCategory));
        if (!Enum.IsDefined(observation.LifecycleState)
            || observation.LifecycleState is AiProviderCallLifecycleState.Prepared or AiProviderCallLifecycleState.DispatchStarted)
        {
            throw new ArgumentOutOfRangeException(nameof(observation), "A terminal provider-call lifecycle state is required.");
        }
    }

    private static void ValidateMetadataLength(string? value, int maximumLength, string parameterName)
    {
        if (value is not null && value.Length > maximumLength)
        {
            throw new ArgumentException($"Provider telemetry metadata exceeds {maximumLength} characters.", parameterName);
        }
    }

    private static string? NormalizeMetadata(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeProvider(string? value)
    {
        value = NormalizeMetadata(value);
        return value is null
            || string.Equals(value, "TimedOut", StringComparison.Ordinal)
            || string.Equals(value, "Exception", StringComparison.Ordinal)
                ? null
                : value;
    }

    private static bool IsProviderCallIdUniqueViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                    && postgresException.ConstraintName == AiProviderCallTelemetryConfiguration.ProviderCallIdUniqueIndexName;
            }

        }

        return false;
    }

    private static bool IsRetryableConcurrencyFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException
                && postgresException.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            {
                return true;
            }

        }

        return false;
    }

    private static void DetachTrackedCall(ApplicationDbContext dbContext, string providerCallId)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<AiProviderCallTelemetry>()
                     .Where(entry => string.Equals(entry.Entity.ProviderCallId, providerCallId, StringComparison.Ordinal))
                     .ToArray())
        {
            entry.State = EntityState.Detached;
        }
    }
}
