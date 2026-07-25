using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface IConversationChannelSender
{
    GuestChannel Channel { get; }

    Task SendAsync(Conversation conversation, ConversationMessage message, CancellationToken cancellationToken);
}