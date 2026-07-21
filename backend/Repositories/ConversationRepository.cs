using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Conversations;
<<<<<<< HEAD
using StayFlow.Api.DTOs.ReservationContext;
=======
>>>>>>> 297967c (Implement host conversation inbox endpoint)
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class ConversationRepository(ApplicationDbContext dbContext) : IConversationRepository
{
<<<<<<< HEAD
    public Task<Conversation?> GetByIdForCompanyAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        return dbContext.Conversations
            .Include(conversation => conversation.Guest)
            .Include(conversation => conversation.Reservation)
            .Include(conversation => conversation.Property)
            .Include(conversation => conversation.AssignedUser)
            .FirstOrDefaultAsync(conversation => conversation.CompanyId == companyId && conversation.Id == conversationId, cancellationToken);
    }

    public Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        return dbContext.Conversations
            .Where(conversation => conversation.CompanyId == companyId
                && conversation.GuestId == guestId
                && conversation.Channel == channel
                && conversation.Status != ConversationStatus.Closed
                && conversation.LastActivityAt >= cutoff)
            .Where(conversation => conversation.ChannelIdentity == channelIdentity)
            .OrderByDescending(conversation => conversation.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid companyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken)
    {
        var pageNumber = query.NormalizedPageNumber;
        var pageSize = query.NormalizedPageSize;
        var messagesQuery = dbContext.ConversationMessages
            .AsNoTracking()
            .Where(message => message.CompanyId == companyId && message.ConversationId == conversationId);

        if (!query.IncludeInternal)
        {
            messagesQuery = messagesQuery.Where(message => !message.IsInternal);
        }

        var totalCount = await messagesQuery.CountAsync(cancellationToken);
        var items = await messagesQuery
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ConversationMessage>
        {
            Items = items,
            PageNumber = pageNumber,
=======
    private static readonly string[] HostAttentionStatuses =
    [
        ConversationStatus.AwaitingHost.ToString(),
        ConversationStatus.Escalated.ToString(),
        ConversationStatus.HumanManaged.ToString()
    ];

    public async Task<PagedResult<ConversationSummaryResponse>> GetInboxAsync(
        Guid companyId,
        ConversationListQueryParameters query,
        CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.NormalizedPageSize;
        var conversationsQuery = dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.CompanyId == companyId);

        if (query.Status is { } status)
        {
            var statusValue = status.ToString();
            conversationsQuery = conversationsQuery.Where(conversation => conversation.Status == statusValue);
        }

        if (query.PropertyId is { } propertyId)
        {
            conversationsQuery = conversationsQuery.Where(conversation => conversation.PropertyId == propertyId);
        }

        if (query.RequiresHostAttention is { } requiresHostAttention)
        {
            conversationsQuery = conversationsQuery.Where(conversation =>
                HostAttentionStatuses.Contains(conversation.Status) == requiresHostAttention);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim();
            conversationsQuery = conversationsQuery.Where(conversation =>
                EF.Functions.ILike(conversation.Guest.FirstName, $"%{searchTerm}%")
                || EF.Functions.ILike(conversation.Guest.LastName, $"%{searchTerm}%")
                || (conversation.Guest.Email != null && EF.Functions.ILike(conversation.Guest.Email, $"%{searchTerm}%"))
                || EF.Functions.ILike(conversation.Property.Name, $"%{searchTerm}%")
                || (conversation.Reservation != null
                    && conversation.Reservation.ConfirmationNumber != null
                    && EF.Functions.ILike(conversation.Reservation.ConfirmationNumber, $"%{searchTerm}%"))
                || (conversation.ExternalThreadId != null && EF.Functions.ILike(conversation.ExternalThreadId, $"%{searchTerm}%")));
        }

        var totalCount = await conversationsQuery.CountAsync(cancellationToken);
        var rows = await conversationsQuery
            .OrderByDescending(conversation => HostAttentionStatuses.Contains(conversation.Status))
            .ThenByDescending(conversation => conversation.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(conversation => new
            {
                ConversationId = conversation.Id,
                conversation.Status,
                Channel = conversation.Channel,
                Subject = conversation.ExternalThreadId,
                conversation.GuestId,
                conversation.Guest.FirstName,
                conversation.Guest.LastName,
                conversation.Guest.Email,
                conversation.PropertyId,
                PropertyName = conversation.Property.Name,
                ReservationId = conversation.Reservation == null ? null : (Guid?)conversation.Reservation.Id,
                ConfirmationNumber = conversation.Reservation == null ? null : conversation.Reservation.ConfirmationNumber,
                StartedAt = conversation.CreatedAt,
                LastActivityAt = conversation.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        var items = rows.Select(row =>
        {
            var status = ParseStatus(row.Status);
            var requiresHostAttention = IsHostAttentionStatus(row.Status);
            return new ConversationSummaryResponse
            {
                ConversationId = row.ConversationId,
                Status = status,
                Channel = row.Channel,
                Subject = row.Subject,
                Guest = new ConversationGuestSummary
                {
                    GuestId = row.GuestId,
                    FirstName = row.FirstName,
                    LastName = row.LastName,
                    Email = row.Email
                },
                Property = new ConversationPropertySummary
                {
                    PropertyId = row.PropertyId,
                    Name = row.PropertyName
                },
                Reservation = row.ReservationId is null
                    ? null
                    : new ConversationReservationSummary
                    {
                        ReservationId = row.ReservationId.Value,
                        ConfirmationNumber = row.ConfirmationNumber
                    },
                AssignedUser = null,
                HumanTakeoverEnabled = requiresHostAttention,
                RequiresHostAttention = requiresHostAttention,
                EscalationReason = null,
                StartedAt = row.StartedAt,
                LastActivityAt = row.LastActivityAt,
                ClosedAt = status == ConversationStatus.Closed ? row.LastActivityAt : null,
                LatestVisibleMessagePreview = null,
                LatestVisibleMessageSenderType = null,
                LatestVisibleMessageTimestamp = null,
                TotalVisibleMessageCount = 0
            };
        }).ToList();

        return new PagedResult<ConversationSummaryResponse>
        {
            Items = items,
            PageNumber = page,
>>>>>>> 297967c (Implement host conversation inbox endpoint)
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

<<<<<<< HEAD
    public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, CancellationToken cancellationToken)
    {
        return dbContext.ConversationMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(message => message.CompanyId == companyId && message.ExternalMessageId == externalMessageId, cancellationToken);
    }

    public Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken)
    {
        return dbContext.Guests.FirstOrDefaultAsync(guest => guest.CompanyId == companyId && guest.Id == guestId && guest.IsActive, cancellationToken);
    }

    public Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken)
    {
        return dbContext.Reservations.FirstOrDefaultAsync(reservation => reservation.CompanyId == companyId && reservation.Id == reservationId && reservation.IsActive, cancellationToken);
    }

    public Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
    {
        return dbContext.Properties.FirstOrDefaultAsync(property => property.CompanyId == companyId && property.Id == propertyId && property.IsActive, cancellationToken);
    }

    public Task<User?> GetUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Users.FirstOrDefaultAsync(user => user.CompanyId == companyId && user.Id == userId && user.IsActive, cancellationToken);
    }

    public async Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        await dbContext.Conversations.AddAsync(conversation, cancellationToken);
    }

    public async Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
    {
        await dbContext.ConversationMessages.AddAsync(message, cancellationToken);
    }

    public async Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
=======
    private static bool IsHostAttentionStatus(string status)
    {
        return status == ConversationStatus.AwaitingHost.ToString()
            || status == ConversationStatus.Escalated.ToString()
            || status == ConversationStatus.HumanManaged.ToString();
    }

    private static ConversationStatus ParseStatus(string status)
    {
        return Enum.TryParse<ConversationStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : ConversationStatus.Open;
>>>>>>> 297967c (Implement host conversation inbox endpoint)
    }
}
