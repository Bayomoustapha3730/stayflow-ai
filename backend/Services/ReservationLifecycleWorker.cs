using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services;

public sealed class ReservationLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReservationLifecycleEventOptions> options,
    ILogger<ReservationLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation("Reservation lifecycle worker is disabled.");
            return;
        }

        var iteration = 0L;
        logger.LogInformation(
            "Reservation lifecycle worker started. PollingIntervalSeconds={PollingIntervalSeconds} GenerationBatchSize={GenerationBatchSize} ProcessingBatchSize={ProcessingBatchSize}.",
            options.Value.PollingIntervalSeconds,
            options.Value.GenerationBatchSize,
            options.Value.ProcessingBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            iteration++;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var generator = scope.ServiceProvider.GetRequiredService<IReservationLifecycleEventGenerator>();
                var processor = scope.ServiceProvider.GetRequiredService<IReservationLifecycleEventProcessor>();

                var generated = await generator.GenerateAsync(stoppingToken);
                var processing = await processor.ProcessDueAsync(stoppingToken);

                if (generated > 0 || processing.Claimed > 0 || processing.StaleRecovered > 0 || processing.FailedRecovered > 0)
                {
                    logger.LogInformation(
                        "Reservation lifecycle worker iteration {WorkerIteration} completed. Generated={Generated} Claimed={Claimed} Processed={Processed} Failed={Failed} Suppressed={Suppressed} StaleRecovered={StaleRecovered} FailedRecovered={FailedRecovered}.",
                        iteration,
                        generated,
                        processing.Claimed,
                        processing.Processed,
                        processing.Failed,
                        processing.Suppressed,
                        processing.StaleRecovered,
                        processing.FailedRecovered);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in reservation lifecycle worker. WorkerIteration={WorkerIteration}.", iteration);
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

        logger.LogInformation("Reservation lifecycle worker stopped.");
    }
}