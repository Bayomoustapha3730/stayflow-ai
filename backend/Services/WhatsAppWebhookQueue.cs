using System.Threading.Channels;

namespace StayFlow.Api.Services;

public sealed class WhatsAppWebhookQueue : IWhatsAppWebhookQueue
{
    private readonly Channel<QueuedWhatsAppWebhookEnvelope> channel = Channel.CreateUnbounded<QueuedWhatsAppWebhookEnvelope>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(QueuedWhatsAppWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        return channel.Writer.WriteAsync(envelope, cancellationToken);
    }

    public ValueTask<QueuedWhatsAppWebhookEnvelope> DequeueAsync(CancellationToken cancellationToken)
    {
        return channel.Reader.ReadAsync(cancellationToken);
    }
}