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
    IWhatsAppOutboundSendGate outboundSendGate,
    IPhoneNumberNormalizer phoneNumberNormalizer,
    ILogger<WhatsAppConversationChannelSender> logger) : IConversationChannelSender
{
    public GuestChannel Channel => GuestChannel.WhatsApp;

    public async Task SendAsync(Conversation conversation, ConversationMessage message, WhatsAppSendOrigin origin, CancellationToken cancellationToken)
    {
        WhatsAppIntegration? integration;
        if (conversation.WhatsAppIntegrationId is { } boundIntegrationId)
        {
            integration = await whatsAppRepository.GetIntegrationForCompanyAsync(conversation.CompanyId, boundIntegrationId, cancellationToken);
            if (integration is null || !integration.IsActive)
            {
                message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
                message.FailedAt = DateTimeOffset.UtcNow;
                message.FailureCode = "IntegrationNotBoundOrInactive";
                message.FailureReason = "The WhatsApp integration bound to this conversation is missing or inactive.";
                message.FailureCategory = "AuthenticationOrConfigurationIssue";
                return;
            }
        }
        else
        {
            // No explicit binding (e.g. a conversation created before this field existed). Only
            // proceed when the company has exactly one active integration; never guess among many.
            integration = await whatsAppRepository.GetSoleActiveIntegrationForCompanyAsync(conversation.CompanyId, cancellationToken);
            if (integration is null)
            {
                message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
                message.FailedAt = DateTimeOffset.UtcNow;
                message.FailureCode = "AmbiguousIntegration";
                message.FailureReason = "This conversation is not bound to a specific WhatsApp integration and the company has zero or multiple active integrations.";
                message.FailureCategory = "AuthenticationOrConfigurationIssue";
                return;
            }
        }

        var gate = outboundSendGate.EvaluateConfiguredSend(origin, integration.IsProductionEnabled);
        if (!gate.Success)
        {
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
            message.FailedAt = DateTimeOffset.UtcNow;
            message.FailureCode = gate.FailureCode;
            message.FailureReason = gate.FailureSummary;
            message.FailureCategory = "AuthenticationOrConfigurationIssue";
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
            CompanyId = conversation.CompanyId,
            IntegrationId = integration.Id,
            IsIntegrationProductionEnabled = integration.IsProductionEnabled,
            Origin = origin,
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
            message.ProviderRequestId = result.ProviderRequestId;
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
        var mapped = WhatsAppFailureMapper.Map(result.FailureCode, result.FailureReason, result.HttpStatusCode, null, null, result.IsTransientFailure, null);
        message.FailureReason = mapped.Summary;
        message.FailureCategory = mapped.Category;
        message.ProviderRequestId = result.ProviderRequestId;
    }
}