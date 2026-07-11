using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class ChatRepository(ApplicationDbContext dbContext) : IChatRepository
{
    public Task<Conversation?> GetConversationAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        return dbContext.Conversations
            .FirstOrDefaultAsync(conversation => conversation.Id == conversationId && conversation.CompanyId == companyId, cancellationToken);
    }

    public Task<bool> GuestBelongsToCompanyAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken)
    {
        return dbContext.Guests.AnyAsync(guest => guest.Id == guestId && guest.CompanyId == companyId && guest.IsActive, cancellationToken);
    }

    public Task<bool> PropertyBelongsToCompanyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
    {
        return dbContext.Properties.AnyAsync(property => property.Id == propertyId && property.CompanyId == companyId && property.IsActive, cancellationToken);
    }

    public async Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        await dbContext.Conversations.AddAsync(conversation, cancellationToken);
    }

    public async Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
    {
        await dbContext.ConversationMessages.AddAsync(message, cancellationToken);
    }

    public async Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid companyId, ChatHistoryQueryParameters query, CancellationToken cancellationToken)
    {
        var pageNumber = query.NormalizedPageNumber;
        var pageSize = query.NormalizedPageSize;
        var messagesQuery = dbContext.ConversationMessages
            .AsNoTracking()
            .Where(message => message.CompanyId == companyId && message.ConversationId == query.ConversationId)
            .OrderBy(message => message.CreatedAt);

        var totalCount = await messagesQuery.CountAsync(cancellationToken);
        var items = await messagesQuery
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

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
