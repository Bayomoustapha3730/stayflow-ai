using Microsoft.Extensions.Options;
using StayFlow.Api.Services;

namespace StayFlow.Api.Services.Billing;

public sealed class CustomerBillingDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CustomerBillingDeliveryWorkerOptions> options,
    IOptions<CustomerUsageBillingOptions> usageBillingOptions,
    TimeProvider timeProvider,
    ILogger<CustomerBillingDeliveryWorker> logger) : BackgroundService
{
    private long cycleCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerOptions = options.Value;
        if (!IsEnabled(workerOptions))
        {
            logger.LogInformation("Customer billing delivery worker is disabled.");
            return;
        }

        logger.LogInformation(
            "Customer billing delivery worker started. PollIntervalSeconds={PollIntervalSeconds} BatchSize={BatchSize} ReconciliationBatchSize={ReconciliationBatchSize}.",
            workerOptions.PollIntervalSeconds,
            workerOptions.BatchSize,
            workerOptions.ReconciliationBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (IsNonFatal(exception))
            {
                logger.LogError(
                    "Customer billing delivery worker cycle failed. WorkerCycle={WorkerCycle} ExceptionType={ExceptionType}.",
                    Volatile.Read(ref cycleCount),
                    exception.GetType().Name);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(workerOptions.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Customer billing delivery worker stopped.");
    }

    internal async Task ProcessCycleAsync(CancellationToken cancellationToken)
    {
        var workerOptions = options.Value;
        if (!IsEnabled(workerOptions))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var workerCycle = Interlocked.Increment(ref cycleCount);
        var now = timeProvider.GetUtcNow();
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ICustomerBillingDeliveryStore>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICustomerBillingDeliveryDispatcher>();
        var counters = new WorkerCycleCounters();

        var dispatchCandidates = await store.GetDispatchCandidatesAsync(
            workerOptions.BatchSize,
            now,
            TimeSpan.FromSeconds(workerOptions.RetryDelaySeconds),
            cancellationToken);
        counters.DispatchCandidates = dispatchCandidates.Count;

        foreach (var deliveryId in dispatchCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await dispatcher.DispatchAsync(deliveryId, cancellationToken);
                switch (result.Outcome)
                {
                    case CustomerBillingDeliveryDispatcherOutcome.Delivered:
                        counters.DispatchSucceeded++;
                        break;
                    case CustomerBillingDeliveryDispatcherOutcome.Rejected:
                        counters.DispatchRejected++;
                        break;
                    case CustomerBillingDeliveryDispatcherOutcome.RetryableFailure:
                        counters.DispatchRetryable++;
                        break;
                    case CustomerBillingDeliveryDispatcherOutcome.Indeterminate:
                        counters.DispatchIndeterminate++;
                        break;
                    case CustomerBillingDeliveryDispatcherOutcome.NotEligible:
                        counters.DispatchNotEligible++;
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsNonFatal(exception))
            {
                counters.DispatchErrors++;
                logger.LogError(
                    "Customer billing delivery dispatch failed for an item. DeliveryId={DeliveryId} ExceptionType={ExceptionType}.",
                    deliveryId,
                    exception.GetType().Name);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var reconciliationCandidates = await store.GetReconciliationCandidatesAsync(
            workerOptions.ReconciliationBatchSize,
            now,
            TimeSpan.FromSeconds(workerOptions.ReconciliationDelaySeconds),
            cancellationToken);
        counters.ReconciliationCandidates = reconciliationCandidates.Count;

        foreach (var deliveryId in reconciliationCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await dispatcher.ReconcileAsync(deliveryId, cancellationToken);
                if (result.Outcome is CustomerBillingDeliveryDispatcherOutcome.Delivered
                    or CustomerBillingDeliveryDispatcherOutcome.Rejected)
                {
                    counters.ReconciliationResolved++;
                }
                else
                {
                    counters.ReconciliationUnresolved++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsNonFatal(exception))
            {
                counters.ReconciliationUnresolved++;
                logger.LogError(
                    "Customer billing delivery reconciliation failed for an item. DeliveryId={DeliveryId} ExceptionType={ExceptionType}.",
                    deliveryId,
                    exception.GetType().Name);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var abandonedCandidates = await store.GetAbandonedDispatchCandidatesAsync(
            workerOptions.ReconciliationBatchSize,
            now - TimeSpan.FromSeconds(workerOptions.DispatchingRecoveryAgeSeconds),
            cancellationToken);
        counters.StaleDispatchRecoveryCandidates = abandonedCandidates.Count;
        foreach (var deliveryId in abandonedCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await store.MarkAbandonedDispatchIndeterminateAsync(
                        deliveryId,
                        now - TimeSpan.FromSeconds(workerOptions.DispatchingRecoveryAgeSeconds),
                        now,
                        cancellationToken))
                {
                    counters.StaleDispatchRecoveries++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsNonFatal(exception))
            {
                logger.LogError(
                    "Abandoned customer billing delivery recovery failed for an item. DeliveryId={DeliveryId} ExceptionType={ExceptionType}.",
                    deliveryId,
                    exception.GetType().Name);
            }
        }

        logger.LogInformation(
            "Customer billing delivery worker cycle completed. WorkerCycle={WorkerCycle} DispatchCandidates={DispatchCandidates} DispatchSucceeded={DispatchSucceeded} DispatchRejected={DispatchRejected} DispatchRetryable={DispatchRetryable} DispatchIndeterminate={DispatchIndeterminate} DispatchNotEligible={DispatchNotEligible} DispatchErrors={DispatchErrors} ReconciliationCandidates={ReconciliationCandidates} ReconciliationResolved={ReconciliationResolved} ReconciliationUnresolved={ReconciliationUnresolved} StaleDispatchRecoveryCandidates={StaleDispatchRecoveryCandidates} StaleDispatchRecoveries={StaleDispatchRecoveries}.",
            workerCycle,
            counters.DispatchCandidates,
            counters.DispatchSucceeded,
            counters.DispatchRejected,
            counters.DispatchRetryable,
            counters.DispatchIndeterminate,
            counters.DispatchNotEligible,
            counters.DispatchErrors,
            counters.ReconciliationCandidates,
            counters.ReconciliationResolved,
            counters.ReconciliationUnresolved,
            counters.StaleDispatchRecoveryCandidates,
            counters.StaleDispatchRecoveries);
    }

    private bool IsEnabled(CustomerBillingDeliveryWorkerOptions workerOptions) =>
        workerOptions.Enabled && usageBillingOptions.Value.Enabled;

    private static bool IsNonFatal(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException;

    private sealed class WorkerCycleCounters
    {
        public int DispatchCandidates { get; set; }
        public int DispatchSucceeded { get; set; }
        public int DispatchRejected { get; set; }
        public int DispatchRetryable { get; set; }
        public int DispatchIndeterminate { get; set; }
        public int DispatchNotEligible { get; set; }
        public int DispatchErrors { get; set; }
        public int ReconciliationCandidates { get; set; }
        public int ReconciliationResolved { get; set; }
        public int ReconciliationUnresolved { get; set; }
        public int StaleDispatchRecoveryCandidates { get; set; }
        public int StaleDispatchRecoveries { get; set; }
    }
}
