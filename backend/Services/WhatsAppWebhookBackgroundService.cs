namespace StayFlow.Api.Services;

public sealed class WhatsAppWebhookBackgroundService(
    IWhatsAppWebhookQueue queue,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<WhatsAppWebhookBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var envelope = await queue.DequeueAsync(stoppingToken);

            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IWhatsAppWebhookProcessor>();
                await processor.ProcessAsync(envelope.Payload, envelope.CorrelationId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing queued WhatsApp webhook. CorrelationId={CorrelationId}", envelope.CorrelationId);
            }
        }
    }
}