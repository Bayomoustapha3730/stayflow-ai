using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class ReservationLifecycleEventService(
    IReservationLifecycleEventRepository repository,
    IReservationLifecycleEventIdempotencyKeyBuilder idempotencyKeyBuilder,
    TimeProvider timeProvider,
    IOptions<ReservationLifecycleEventOptions> options) : IReservationLifecycleEventService
{
    // Index name is fixed by the AddReservationLifecycleEvents migration.
    private const string IdempotencyKeyUniqueIndexName = "UX_ReservationLifecycleEvents_CompanyId_IdempotencyKey";

    public async Task<ReservationLifecycleEventCreationResult> TryCreateAsync(
        Reservation reservation,
        Property property,
        ReservationLifecycleEventType eventType,
        DateOnly propertyLocalDate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(property);

        if (reservation.PropertyId != property.Id)
        {
            throw new ArgumentException("Reservation must belong to the supplied property.", nameof(property));
        }

        if (reservation.CompanyId != property.CompanyId)
        {
            throw new ArgumentException("Reservation and property must belong to the same company.", nameof(property));
        }

        // Cancelled/no-show reservations no longer have a real guest journey to schedule against;
        // the normal PreArrival/ArrivalDay/InStay/CheckoutDay/PostStay timeline must not continue.
        if (reservation.Status is ReservationStatus.Cancelled or ReservationStatus.NoShow)
        {
            throw new InvalidOperationException(
                $"Cannot schedule a {eventType} lifecycle event for a reservation in {reservation.Status} status.");
        }

        var ruleVersion = ReservationLifecycleRuleVersions.V1;
        var idempotencyKey = idempotencyKeyBuilder.Build(reservation.CompanyId, reservation.Id, eventType, propertyLocalDate, ruleVersion);

        var existing = await repository.GetByIdempotencyKeyAsync(reservation.CompanyId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return new ReservationLifecycleEventCreationResult(existing, false);
        }

        var propertyTimeZone = PropertyTimeZoneResolver.Resolve(property.TimeZone);
        var localDateTime = propertyLocalDate.ToDateTime(options.Value.DefaultLocalTriggerTime, DateTimeKind.Unspecified);
        var scheduledForUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDateTime, propertyTimeZone), TimeSpan.Zero);

        var lifecycleEvent = new ReservationLifecycleEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = reservation.CompanyId,
            ReservationId = reservation.Id,
            PropertyId = property.Id,
            GuestId = reservation.PrimaryGuestId,
            EventType = eventType,
            RuleVersion = ruleVersion,
            PropertyLocalDate = propertyLocalDate,
            ScheduledForUtc = scheduledForUtc,
            Status = ReservationLifecycleEventStatus.Pending,
            AttemptCount = 0,
            IdempotencyKey = idempotencyKey
        };

        await repository.AddAsync(lifecycleEvent, cancellationToken);

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsIdempotencyKeyUniqueViolation(ex))
        {
            // Concurrent insert raced past the pre-check; the unique index is the source of truth.
            repository.Detach(lifecycleEvent);
            var winner = await repository.GetByIdempotencyKeyAsync(reservation.CompanyId, idempotencyKey, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return new ReservationLifecycleEventCreationResult(winner, false);
        }

        return new ReservationLifecycleEventCreationResult(lifecycleEvent, true);
    }

    public Task<ReservationLifecycleEvent?> GetAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken)
    {
        return repository.GetByIdAsync(companyId, eventId, cancellationToken);
    }

    public Task<IReadOnlyCollection<ReservationLifecycleEvent>> GetPendingAsync(Guid companyId, DateTimeOffset dueBeforeUtc, int limit, CancellationToken cancellationToken)
    {
        return repository.GetPendingAsync(companyId, dueBeforeUtc, limit, cancellationToken);
    }

    public async Task<ReservationLifecycleEvent> MarkProcessingAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken)
    {
        var entity = await GetForTenantOrThrowAsync(companyId, eventId, cancellationToken);
        if (entity.Status != ReservationLifecycleEventStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot transition lifecycle event from {entity.Status} to Processing.");
        }

        entity.Status = ReservationLifecycleEventStatus.Processing;
        entity.AttemptCount += 1;
        entity.LastAttemptAtUtc = timeProvider.GetUtcNow();
        await repository.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<ReservationLifecycleEvent> MarkProcessedAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken)
    {
        var entity = await GetForTenantOrThrowAsync(companyId, eventId, cancellationToken);
        if (entity.Status != ReservationLifecycleEventStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot transition lifecycle event from {entity.Status} to Processed.");
        }

        entity.Status = ReservationLifecycleEventStatus.Processed;
        entity.ProcessedAtUtc = timeProvider.GetUtcNow();
        entity.LastError = null;
        await repository.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<ReservationLifecycleEvent> MarkFailedAsync(Guid companyId, Guid eventId, string error, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        var entity = await GetForTenantOrThrowAsync(companyId, eventId, cancellationToken);
        if (entity.Status != ReservationLifecycleEventStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot transition lifecycle event from {entity.Status} to Failed.");
        }

        entity.Status = ReservationLifecycleEventStatus.Failed;
        entity.LastAttemptAtUtc = timeProvider.GetUtcNow();
        entity.LastError = error.Length > 500 ? error[..500] : error;
        await repository.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<ReservationLifecycleEvent> GetForTenantOrThrowAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(companyId, eventId, cancellationToken)
            ?? throw new KeyNotFoundException("Reservation lifecycle event was not found for the authenticated tenant.");
    }

    // Only a genuine 23505 on the lifecycle-event idempotency index means "already created".
    // Every other DbUpdateException (e.g. a primary key collision) is a real persistence
    // failure and must not be masked as a benign duplicate.
    private static bool IsIdempotencyKeyUniqueViolation(Exception ex)
    {
        if (ex is not DbUpdateException dbUpdateException)
        {
            return false;
        }

        return dbUpdateException.GetBaseException() is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgresException.ConstraintName, IdempotencyKeyUniqueIndexName, StringComparison.Ordinal);
    }
}
