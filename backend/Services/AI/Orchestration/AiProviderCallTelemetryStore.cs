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
            Model = NormalizeMetadata(observation.Model),
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
        var model = MergeMetadata(existing.Model, observation.Model, observation.ProviderCallId, "model");

        if (existing.Outcome != observation.Outcome)
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
        existing.Model = model;
        if (observation.TokenUsage is not { } knownUsage || existing.UsageKnown)
        {
            return;
        }

        existing.UsageKnown = true;
        existing.InputTokens = knownUsage.InputTokens;
        existing.OutputTokens = knownUsage.OutputTokens;
        existing.TotalTokens = knownUsage.TotalTokens;
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
        ValidateMetadataLength(observation.Model, 120, nameof(observation.Model));
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
