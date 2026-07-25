using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Services;

public interface IWhatsAppCloudClient
{
    Task<WhatsAppSendTextMessageResult> SendTextMessageAsync(WhatsAppSendTextMessageRequest request, CancellationToken cancellationToken);
    Task<WhatsAppGetTemplatesResult> GetTemplatesAsync(WhatsAppGetTemplatesRequest request, CancellationToken cancellationToken);
    Task<WhatsAppSendTemplateMessageResult> SendTemplateMessageAsync(WhatsAppTemplateSendRequest request, CancellationToken cancellationToken);
    Task<WhatsAppValidateIntegrationResult> ValidateIntegrationAsync(WhatsAppValidateIntegrationRequest request, CancellationToken cancellationToken);
}