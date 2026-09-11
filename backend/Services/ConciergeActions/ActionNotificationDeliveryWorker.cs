using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ActionNotificationDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ActionNotificationDeliveryOptions> options,
    ILogger<ActionNotificationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation("Action notification delivery worker is disabled.");
            return;
        }

        var iteration = 0L;
        logger.LogInformation(
            "Action notification delivery worker started. PollingIntervalSeconds={PollingIntervalSeconds} BatchSize={BatchSize}.",
            options.Value.PollingIntervalSeconds,
            options.Value.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            iteration++;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IActionNotificationDeliveryProcessor>();
                var result = await processor.ProcessDueAsync(stoppingToken);

                if (result.Claimed > 0)
                {
                    logger.LogInformation(
                        "Action notification delivery worker iteration {WorkerIteration} completed. Claimed={Claimed} Sent={Sent} Failed={Failed} Skipped={Skipped}.",
                        iteration,
                        result.Claimed,
                        result.Sent,
                        result.Failed,
                        result.Skipped);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in action notification delivery worker. WorkerIteration={WorkerIteration}.", iteration);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Action notification delivery worker stopped.");
    }
}
