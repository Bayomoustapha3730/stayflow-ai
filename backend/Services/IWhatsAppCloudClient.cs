using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Services;

public interface IWhatsAppCloudClient
{
    Task<WhatsAppSendTextMessageResult> SendTextMessageAsync(WhatsAppSendTextMessageRequest request, CancellationToken cancellationToken);
}