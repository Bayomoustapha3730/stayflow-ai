using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class WhatsAppConversationChannelSender(
    IWhatsAppCloudClient whatsAppCloudClient,
    IWhatsAppRepository whatsAppRepository,
    IWhatsAppCredentialResolver credentialResolver,
    IWhatsAppCustomerServiceWindowEvaluator customerServiceWindowEvaluator,
    IHostEnvironment environment,
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
            message.FailureCategory = "AuthenticationOrConfigurationIssue";
            return;
        }

        if (!integration.IsProductionEnabled && !environment.IsDevelopment())
        {
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
            message.FailedAt = DateTimeOffset.UtcNow;
            message.FailureCode = "ProductionDisabled";
            message.FailureReason = "WhatsApp sending is unavailable. Contact an administrator.";
            return;
        }

        if (!message.IsTemplateMessage)
        {
            var window = await customerServiceWindowEvaluator.EvaluateAsync(conversation.CompanyId, conversation.Id, cancellationToken);
            if (!window.IsOpen)
            {
                message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
                message.FailedAt = DateTimeOffset.UtcNow;
                message.FailureCode = "CustomerServiceWindowClosed";
                message.FailureReason = "The WhatsApp customer-service window is closed. Send an approved template to restart the conversation.";
                return;
            }
        }

        var credentials = await credentialResolver.ResolveAsync(integration, cancellationToken);
        if (!credentials.Success || string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
            message.FailedAt = DateTimeOffset.UtcNow;
            message.FailureCode = "CredentialResolutionFailed";
            message.FailureReason = credentials.FailureSummary ?? "WhatsApp sending is unavailable. Contact an administrator.";
            return;
        }

        if (!phoneNumberNormalizer.TryNormalize(conversation.ChannelIdentity, out var normalizedRecipient))
        {
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
            message.FailedAt = DateTimeOffset.UtcNow;
            message.FailureCode = "InvalidRecipient";
            message.FailureReason = "Conversation channel identity is not a valid WhatsApp destination.";
            message.FailureCategory = "InvalidDestination";
            return;
        }

        var result = await whatsAppCloudClient.SendTextMessageAsync(new WhatsAppSendTextMessageRequest
        {
            AccessToken = credentials.AccessToken,
            GraphApiVersion = integration.GraphApiVersion,
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
            message.FailureCategory = null;
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
<<<<<<< HEAD
        message.FailureReason = result.IsTransientFailure
            ? "WhatsApp is temporarily unavailable. Try again."
            : "WhatsApp could not deliver this message.";
=======
        message.FailureReason = result.FailureReason;
        var mapped = WhatsAppFailureMapper.Map(result.FailureCode, result.FailureReason);
        message.FailureCategory = mapped.Category;
>>>>>>> origin/main
    }
}