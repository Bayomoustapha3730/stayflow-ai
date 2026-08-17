using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services.Payments;

public sealed class MpesaPaymentReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MpesaOptions> options,
    ILogger<MpesaPaymentReconciliationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled ||
            !options.Value.ReconciliationEnabled)
        {
            logger.LogInformation(
                "M-PESA payment reconciliation worker is disabled.");
            return;
        }

        logger.LogInformation(
            "M-PESA payment reconciliation worker started. " +
            "PendingAge={PendingAgeSeconds}s ScanInterval={ScanIntervalSeconds}s.",
            options.Value.ReconciliationPendingAgeSeconds,
            options.Value.ReconciliationScanIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                var reconciliationService =
                    scope.ServiceProvider.GetRequiredService<
                        IMpesaPaymentReconciliationService>();

                var reconciled =
                    await reconciliationService.ReconcileStalePaymentsAsync(
                        stoppingToken);

                if (reconciled > 0)
                {
                    logger.LogInformation(
                        "M-PESA reconciliation cycle updated {Count} payment(s).",
                        reconciled);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected error in M-PESA reconciliation worker.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        options.Value.ReconciliationScanIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation(
            "M-PESA payment reconciliation worker stopped.");
    }
}
