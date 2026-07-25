using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Services;

public interface IWhatsAppWebhookQueue
{
    ValueTask EnqueueAsync(QueuedWhatsAppWebhookEnvelope envelope, CancellationToken cancellationToken);
    ValueTask<QueuedWhatsAppWebhookEnvelope> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class QueuedWhatsAppWebhookEnvelope
{
    public string CorrelationId { get; init; } = string.Empty;
    public WhatsAppWebhookPayload Payload { get; init; } = null!;
}