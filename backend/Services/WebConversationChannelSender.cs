using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class WebConversationChannelSender : IConversationChannelSender
{
    public GuestChannel Channel => GuestChannel.Web;

    public Task SendAsync(Conversation conversation, ConversationMessage message, WhatsAppSendOrigin origin, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}