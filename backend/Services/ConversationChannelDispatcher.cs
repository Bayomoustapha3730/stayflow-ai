using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class ConversationChannelDispatcher(
    IEnumerable<IConversationChannelSender> senders,
    IConversationRepository conversationRepository,
    IConversationRealtimePublisher realtimePublisher) : IConversationChannelDispatcher
{
    private readonly Dictionary<GuestChannel, IConversationChannelSender> sendersByChannel = senders
        .GroupBy(sender => sender.Channel)
        .ToDictionary(group => group.Key, group => group.Last());

    public async Task DispatchOutboundMessageAsync(Conversation conversation, ConversationMessage message, CancellationToken cancellationToken)
    {
        if (!sendersByChannel.TryGetValue(conversation.Channel, out var sender))
        {
            return;
        }

        await sender.SendAsync(conversation, message, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);
        var safeFailureSummary = message.DeliveryStatus == ConversationMessageDeliveryStatus.Failed
            ? WhatsAppFailureMapper.Map(message.FailureCode, message.FailureReason).Summary
            : null;

        await realtimePublisher.PublishMessageUpdatedAsync(conversation.CompanyId, conversation.Id, new
        {
            conversationId = conversation.Id,
            message = new DTOs.Conversations.ConversationMessageResponse
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderType = message.SenderType,
                MessageType = message.MessageType,
                Content = message.Content,
                IsInternal = message.IsInternal,
                Provider = message.Provider,
                DeliveryStatus = message.DeliveryStatus,
                DeliveredAt = message.DeliveredAt,
                ReadAt = message.ReadAt,
                FailedAt = message.FailedAt,
                SafeFailureSummary = safeFailureSummary,
                RetryOfMessageId = message.RetryOfMessageId,
                SendAttemptNumber = message.SendAttemptNumber,
                CanRetry = message.DeliveryStatus == ConversationMessageDeliveryStatus.Failed
                    && !message.IsInternal
                    && message.Provider == ConversationMessageProvider.WhatsAppCloud
                    && message.SenderType is ConversationSenderType.Host or ConversationSenderType.AI,
                SentAt = message.SentAt
            },
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

        if (message.DeliveryStatus is not null)
        {
            await realtimePublisher.PublishMessageDeliveryUpdatedAsync(conversation.CompanyId, conversation.Id, new
            {
                conversationId = conversation.Id,
                messageId = message.Id,
                deliveryStatus = message.DeliveryStatus,
                deliveredAt = message.DeliveredAt,
                readAt = message.ReadAt,
                failedAt = message.FailedAt,
                safeFailureSummary
            }, cancellationToken);
        }
    }
}