using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class ConversationRepository(ApplicationDbContext dbContext) : IConversationRepository
{
    public async Task<PagedResult<ConversationSummaryResponse>> ListConversationsAsync(Guid companyId, ConversationListQueryParameters query, CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.NormalizedPageSize;
        var conversationsQuery = dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.CompanyId == companyId && !conversation.IsDeleted);

        if (query.Status is { } status)
        {
            conversationsQuery = conversationsQuery.Where(conversation => conversation.Status == status);
        }

        if (query.PropertyId is { } propertyId)
        {
            conversationsQuery = conversationsQuery.Where(conversation => conversation.PropertyId == propertyId);
        }

        if (query.RequiresHostAttention is { } requiresHostAttention)
        {
            conversationsQuery = conversationsQuery.Where(conversation =>
                (conversation.HumanTakeoverEnabled
                    || conversation.Status == ConversationStatus.AwaitingHost
                    || conversation.Status == ConversationStatus.Escalated
                    || conversation.Status == ConversationStatus.HumanManaged) == requiresHostAttention);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim();
            conversationsQuery = conversationsQuery.Where(conversation =>
                EF.Functions.ILike(conversation.Guest.FirstName, $"%{searchTerm}%")
                || EF.Functions.ILike(conversation.Guest.LastName, $"%{searchTerm}%")
                || (conversation.Guest.Email != null && EF.Functions.ILike(conversation.Guest.Email, $"%{searchTerm}%"))
                || (conversation.Property != null && EF.Functions.ILike(conversation.Property.Name, $"%{searchTerm}%"))
                || (conversation.Reservation != null
                    && conversation.Reservation.ConfirmationNumber != null
                    && EF.Functions.ILike(conversation.Reservation.ConfirmationNumber, $"%{searchTerm}%"))
                || (conversation.Subject != null && EF.Functions.ILike(conversation.Subject, $"%{searchTerm}%")));
        }

        var totalCount = await conversationsQuery.CountAsync(cancellationToken);
        var items = await conversationsQuery
            .OrderByDescending(conversation =>
                conversation.HumanTakeoverEnabled
                || conversation.Status == ConversationStatus.AwaitingHost
                || conversation.Status == ConversationStatus.Escalated
                || conversation.Status == ConversationStatus.HumanManaged)
            .ThenByDescending(conversation => conversation.LastActivityAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(conversation => new ConversationSummaryResponse
            {
                Id = conversation.Id,
                ConversationId = conversation.Id,
                GuestId = conversation.GuestId,
                ReservationId = conversation.ReservationId,
                PropertyId = conversation.PropertyId,
                Channel = conversation.Channel,
                ChannelIdentity = conversation.ChannelIdentity,
                Status = conversation.Status,
                Subject = conversation.Subject,
                HumanTakeoverEnabled = conversation.HumanTakeoverEnabled,
                RequiresHostAttention = conversation.HumanTakeoverEnabled
                    || conversation.Status == ConversationStatus.AwaitingHost
                    || conversation.Status == ConversationStatus.Escalated
                    || conversation.Status == ConversationStatus.HumanManaged,
                EscalationReason = conversation.EscalationReason,
                StartedAt = conversation.StartedAt,
                LastActivityAt = conversation.LastActivityAt,
                ClosedAt = conversation.ClosedAt,
                Guest = new ConversationGuestSummary
                {
                    Id = conversation.GuestId,
                    FirstName = conversation.Guest.FirstName,
                    LastName = conversation.Guest.LastName,
                    FullName = conversation.Guest.FirstName + " " + conversation.Guest.LastName,
                    Email = conversation.Guest.Email,
                    PreferredLanguage = conversation.Guest.PreferredLanguage
                },
                Property = conversation.Property == null
                    ? null
                    : new ConversationPropertySummary
                    {
                        Id = conversation.Property.Id,
                        Name = conversation.Property.Name,
                        City = conversation.Property.City
                    },
                Reservation = conversation.Reservation == null
                    ? null
                    : new ConversationReservationSummary
                    {
                        Id = conversation.Reservation.Id,
                        ConfirmationNumber = conversation.Reservation.ConfirmationNumber,
                        CheckInDate = conversation.Reservation.CheckInDate,
                        CheckOutDate = conversation.Reservation.CheckOutDate,
                        Status = conversation.Reservation.Status
                    },
                AssignedUser = conversation.AssignedUser == null
                    ? null
                    : new ConversationAssignedUserSummary
                    {
                        Id = conversation.AssignedUser.Id,
                        FullName = conversation.AssignedUser.FullName
                    },
                LatestVisibleMessagePreview = conversation.Messages
                    .Where(message => !message.IsInternal && !message.IsDeleted)
                    .OrderByDescending(message => message.SentAt)
                    .ThenByDescending(message => message.CreatedAt)
                    .Select(message => message.Content)
                    .FirstOrDefault(),
                LatestVisibleMessageSenderType = conversation.Messages
                    .Where(message => !message.IsInternal && !message.IsDeleted)
                    .OrderByDescending(message => message.SentAt)
                    .ThenByDescending(message => message.CreatedAt)
                    .Select(message => (ConversationSenderType?)message.SenderType)
                    .FirstOrDefault(),
                LatestVisibleMessageTimestamp = conversation.Messages
                    .Where(message => !message.IsInternal && !message.IsDeleted)
                    .OrderByDescending(message => message.SentAt)
                    .ThenByDescending(message => message.CreatedAt)
                    .Select(message => (DateTimeOffset?)message.SentAt)
                    .FirstOrDefault(),
                TotalVisibleMessageCount = conversation.Messages.Count(message => !message.IsInternal && !message.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ConversationSummaryResponse>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

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

    public async Task<int> GetTotalUnreadCountForHostAsync(Guid companyId, Guid hostUserId, CancellationToken cancellationToken)
    {
        var readStates = dbContext.ConversationParticipantReadStates
            .Where(state => state.CompanyId == companyId
                && state.ParticipantKind == ConversationParticipantKind.HostUser
                && state.ParticipantId == hostUserId);

        var query = from message in dbContext.ConversationMessages
                    join conversation in dbContext.Conversations on message.ConversationId equals conversation.Id
                    join readState in readStates on message.ConversationId equals readState.ConversationId into readStateGroup
                    from state in readStateGroup.DefaultIfEmpty()
                    where message.CompanyId == companyId
                          && conversation.CompanyId == companyId
                          && !message.IsInternal
                          && !message.IsDeleted
                          && !conversation.IsDeleted
                          && message.SenderType == ConversationSenderType.Guest
                          && (state == null || message.SentAt > state.LastReadAt)
                    select message.Id;

        return await query.CountAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, int>> GetUnreadMessageCountsForHostAsync(
        Guid companyId,
        Guid hostUserId,
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken)
    {
        if (conversationIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var conversationIdSet = conversationIds.ToHashSet();
        var readStates = dbContext.ConversationParticipantReadStates
            .Where(state => state.CompanyId == companyId
                && state.ParticipantKind == ConversationParticipantKind.HostUser
                && state.ParticipantId == hostUserId);

        var unreadQuery = from message in dbContext.ConversationMessages
                          join readState in readStates on message.ConversationId equals readState.ConversationId into readStateGroup
                          from state in readStateGroup.DefaultIfEmpty()
                          where message.CompanyId == companyId
                                && conversationIdSet.Contains(message.ConversationId)
                                && !message.IsInternal
                                && !message.IsDeleted
                                && message.SenderType == ConversationSenderType.Guest
                                && (state == null || message.SentAt > state.LastReadAt)
                          group message by message.ConversationId into conversationGroup
                          select new
                          {
                              ConversationId = conversationGroup.Key,
                              Count = conversationGroup.Count()
                          };

        return await unreadQuery.ToDictionaryAsync(item => item.ConversationId, item => item.Count, cancellationToken);
    }

    public async Task<int> GetUnreadHostMessageCountForGuestAsync(Guid companyId, Guid guestId, Guid conversationId, CancellationToken cancellationToken)
    {
        var readState = await dbContext.ConversationParticipantReadStates
            .AsNoTracking()
            .FirstOrDefaultAsync(state => state.CompanyId == companyId
                && state.ConversationId == conversationId
                && state.ParticipantKind == ConversationParticipantKind.Guest
                && state.ParticipantId == guestId, cancellationToken);

        var unreadQuery = dbContext.ConversationMessages
            .Where(message => message.CompanyId == companyId
                && message.ConversationId == conversationId
                && !message.IsInternal
                && !message.IsDeleted
                && message.SenderType == ConversationSenderType.Host);

        if (readState is not null)
        {
            unreadQuery = unreadQuery.Where(message => message.SentAt > readState.LastReadAt);
        }

        return await unreadQuery.CountAsync(cancellationToken);
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
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<ConversationMessage?> GetLatestVisibleMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        return dbContext.ConversationMessages
            .AsNoTracking()
            .Where(message => message.CompanyId == companyId
                && message.ConversationId == conversationId
                && !message.IsDeleted
                && !message.IsInternal)
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, CancellationToken cancellationToken)
    {
        return dbContext.ConversationMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(message => message.CompanyId == companyId && message.ExternalMessageId == externalMessageId, cancellationToken);
    }

    public Task<ConversationParticipantReadState?> GetReadStateAsync(
        Guid companyId,
        Guid conversationId,
        ConversationParticipantKind participantKind,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        return dbContext.ConversationParticipantReadStates.FirstOrDefaultAsync(state =>
            state.CompanyId == companyId
            && state.ConversationId == conversationId
            && state.ParticipantKind == participantKind
            && state.ParticipantId == participantId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ConversationParticipantReadState>> GetReadStatesForParticipantAsync(
        Guid companyId,
        ConversationParticipantKind participantKind,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ConversationParticipantReadStates
            .AsNoTracking()
            .Where(state => state.CompanyId == companyId
                && state.ParticipantKind == participantKind
                && state.ParticipantId == participantId)
            .ToListAsync(cancellationToken);
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

    public async Task AddReadStateAsync(ConversationParticipantReadState state, CancellationToken cancellationToken)
    {
        await dbContext.ConversationParticipantReadStates.AddAsync(state, cancellationToken);
    }

    public async Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
