using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services;

public sealed class GuestJourneyDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<GuestJourneyDeliveryOptions> options,
    ILogger<GuestJourneyDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation("Guest journey delivery worker is disabled.");
            return;
        }

        var iteration = 0L;
        logger.LogInformation(
            "Guest journey delivery worker started. PollingIntervalSeconds={PollingIntervalSeconds} BatchSize={BatchSize}.",
            options.Value.PollingIntervalSeconds,
            options.Value.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            iteration++;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IGuestJourneyMessageDeliveryProcessor>();
                var result = await processor.ProcessDueAsync(stoppingToken);

                if (result.Claimed > 0 || result.StaleRecovered > 0 || result.FailedRecovered > 0)
                {
                    logger.LogInformation(
                        "Guest journey delivery worker iteration {WorkerIteration} completed. Claimed={Claimed} Accepted={Accepted} Failed={Failed} Suppressed={Suppressed} Blocked={Blocked} StaleRecovered={StaleRecovered} FailedRecovered={FailedRecovered}.",
                        iteration,
                        result.Claimed,
                        result.Accepted,
                        result.Failed,
                        result.Suppressed,
                        result.Blocked,
                        result.StaleRecovered,
                        result.FailedRecovered);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in guest journey delivery worker. WorkerIteration={WorkerIteration}.", iteration);
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

        logger.LogInformation("Guest journey delivery worker stopped.");
    }
}
