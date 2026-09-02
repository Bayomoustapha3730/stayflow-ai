using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class GuestJourneyMessageService(IGuestJourneyMessageRepository repository) : IGuestJourneyMessageService
{
    // Index names are fixed by the AddGuestJourneyMessages migration. Both constraints describe the
    // same logical duplicate for a lifecycle event (IdempotencyKey mirrors the event's key), and
    // PostgreSQL may report either one, so both must be treated as a benign concurrent duplicate.
    private const string LifecycleEventUniqueIndexName = "UX_GuestJourneyMessages_CompanyId_ReservationLifecycleEventId";
    private const string IdempotencyKeyUniqueIndexName = "IX_GuestJourneyMessages_CompanyId_IdempotencyKey";

    public async Task<GuestJourneyMessageCreationResult> TryCreateAsync(
        ReservationLifecycleEvent lifecycleEvent,
        string language,
        string renderedContent,
        Guid? conversationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(renderedContent);

        var existing = await repository.GetByLifecycleEventAsync(lifecycleEvent.CompanyId, lifecycleEvent.Id, cancellationToken);
        if (existing is not null)
        {
            return new GuestJourneyMessageCreationResult(existing, false);
        }

        var message = new GuestJourneyMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = lifecycleEvent.CompanyId,
            ReservationId = lifecycleEvent.ReservationId,
            ReservationLifecycleEventId = lifecycleEvent.Id,
            PropertyId = lifecycleEvent.PropertyId,
            GuestId = lifecycleEvent.GuestId,
            ConversationId = conversationId,
            JourneyEventType = lifecycleEvent.EventType,
            Channel = GuestJourneyMessageChannel.WhatsApp,
            Language = language,
            ContentType = GuestJourneyMessageContentType.Text,
            RenderedContent = renderedContent,
            Status = GuestJourneyMessageStatus.Pending,
            AttemptCount = 0,
            // Mirrors the originating lifecycle event's key 1:1 since CompanyId + ReservationLifecycleEventId
            // is already the primary uniqueness guarantee; this participates as the secondary constraint.
            IdempotencyKey = lifecycleEvent.IdempotencyKey
        };

        await repository.AddAsync(message, cancellationToken);

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsLifecycleEventUniqueViolation(ex))
        {
            // Concurrent insert raced past the pre-check; the unique index is the source of truth.
            repository.Detach(message);
            var winner = await repository.GetByLifecycleEventAsync(lifecycleEvent.CompanyId, lifecycleEvent.Id, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return new GuestJourneyMessageCreationResult(winner, false);
        }

        return new GuestJourneyMessageCreationResult(message, true);
    }

    // Only a genuine 23505 on a GuestJourneyMessage uniqueness index means "already created". Every
    // other DbUpdateException is a real persistence failure and must not be masked as benign.
    private static bool IsLifecycleEventUniqueViolation(Exception ex)
    {
        if (ex is not DbUpdateException dbUpdateException)
        {
            return false;
        }

        return dbUpdateException.GetBaseException() is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && (string.Equals(postgresException.ConstraintName, LifecycleEventUniqueIndexName, StringComparison.Ordinal)
                || string.Equals(postgresException.ConstraintName, IdempotencyKeyUniqueIndexName, StringComparison.Ordinal));
    }
}
