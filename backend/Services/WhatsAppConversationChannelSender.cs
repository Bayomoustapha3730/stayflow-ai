using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class WhatsAppConversationChannelSender(
    IWhatsAppCloudClient whatsAppCloudClient,
    IWhatsAppRepository whatsAppRepository,
    IPhoneNumberNormalizer phoneNumberNormalizer,
    ILogger<WhatsAppConversationChannelSender> logger) : IConversationChannelSender
{
    public GuestChannel Channel => GuestChannel.WhatsApp;

    public async Task SendAsync(Conversation conversation, ConversationMessage message, CancellationToken cancellationToken)
    {
        var integration = await whatsAppRepository.GetActiveIntegrationByCompanyIdAsync(conversation.CompanyId, cancellationToken);
        if (integration is null)
        {
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
            message.FailedAt = DateTimeOffset.UtcNow;
            message.FailureCode = "MissingIntegration";
            message.FailureReason = "WhatsApp integration is not configured for this company.";
            return;
        }

        if (!phoneNumberNormalizer.TryNormalize(conversation.ChannelIdentity, out var normalizedRecipient))
        {
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
            message.FailedAt = DateTimeOffset.UtcNow;
            message.FailureCode = "InvalidRecipient";
            message.FailureReason = "Conversation channel identity is not a valid WhatsApp destination.";
            return;
        }

        var result = await whatsAppCloudClient.SendTextMessageAsync(new WhatsAppSendTextMessageRequest
        {
            PhoneNumberId = integration.PhoneNumberId,
            To = normalizedRecipient,
            Body = message.Content,
            ClientMessageId = message.Id.ToString("N")
        }, cancellationToken);

        if (result.Success)
        {
            message.Provider = ConversationMessageProvider.WhatsAppCloud;
            message.ExternalMessageId = result.ExternalMessageId ?? message.ExternalMessageId;
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Sent;
            message.DeliveredAt = null;
            message.ReadAt = null;
            message.FailedAt = null;
            message.FailureCode = null;
            message.FailureReason = null;
            return;
        }

        logger.LogWarning(
            "WhatsApp outbound delivery failed. CompanyId={CompanyId} ConversationId={ConversationId} MessageId={MessageId}",
            conversation.CompanyId,
            conversation.Id,
            message.Id);

        message.Provider = ConversationMessageProvider.WhatsAppCloud;
        message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
        message.FailedAt = DateTimeOffset.UtcNow;
        message.FailureCode = result.FailureCode;
        message.FailureReason = result.FailureReason;
    }
}