using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IChatRepository
{
    Task<Conversation?> GetConversationAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken);
    Task<bool> GuestBelongsToCompanyAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken);
    Task<bool> PropertyBelongsToCompanyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken);
    Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken);
    Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken);
    Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid companyId, ChatHistoryQueryParameters query, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
