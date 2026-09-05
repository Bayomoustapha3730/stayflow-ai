using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface IConversationChannelDispatcher
{
    Task DispatchOutboundMessageAsync(Conversation conversation, ConversationMessage message, WhatsAppSendOrigin origin, CancellationToken cancellationToken);
}