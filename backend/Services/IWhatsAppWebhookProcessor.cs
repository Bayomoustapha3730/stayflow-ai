using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Services;

public interface IWhatsAppWebhookProcessor
{
    Task ProcessAsync(WhatsAppWebhookPayload payload, string correlationId, CancellationToken cancellationToken);
}