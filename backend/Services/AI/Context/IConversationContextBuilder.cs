namespace StayFlow.Api.Services.AI.Context;

public interface IConversationContextBuilder
{
    Task<ConversationContext?> BuildAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken);
}
