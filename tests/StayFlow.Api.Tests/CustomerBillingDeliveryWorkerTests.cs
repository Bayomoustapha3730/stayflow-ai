using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Tests;

public sealed class CustomerBillingDeliveryWorkerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckedInConfiguration_KeepsBothActivationSwitchesOff(bool includeDevelopment)
    {
        var builder = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false);
        if (includeDevelopment)
        {
            builder.AddJsonFile("appsettings.Development.json", optional: false);
        }

        var configuration = builder.Build();
        var usage = configuration.GetSection(CustomerUsageBillingOptions.SectionName)
            .Get<CustomerUsageBillingOptions>()!;
        var worker = configuration.GetSection(CustomerBillingDeliveryWorkerOptions.SectionName)
            .Get<CustomerBillingDeliveryWorkerOptions>()!;
        Assert.False(usage.Enabled);
        Assert.False(worker.Enabled);
        Assert.False(usage.OpenPeriodInvoiceSafetyVerified);
        Assert.Empty(usage.Metrics);
        Assert.Equal(BillingOptions.SupportedStripeApiVersion, configuration["Billing:StripeApiVersion"]);
    }

    [Fact]
    public void WorkerDependsOnDispatcherInterfacesNotStripeTransport()
    {
        var constructorParameterTypes = typeof(CustomerBillingDeliveryWorker)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);

        Assert.DoesNotContain(typeof(StripeCustomerBillingTransport), constructorParameterTypes);
        Assert.DoesNotContain(typeof(StripeBillingProvider), constructorParameterTypes);
    }

    [Fact]
    public async Task DisabledWorkerDoesNotQueryOrDispatch()
    {
        var store = new RecordingStore();
        var dispatcher = new RecordingDispatcher();
        await using var provider = CreateProvider(store, dispatcher);
        var worker = CreateWorker(provider, new CustomerBillingDeliveryWorkerOptions());

        await worker.ProcessCycleAsync(CancellationToken.None);

        Assert.Equal(0, store.DispatchQueries);
        Assert.Equal(0, store.ReconciliationQueries);
        Assert.Equal(0, store.AbandonedQueries);
        Assert.Empty(dispatcher.Dispatched);
        Assert.Empty(dispatcher.Reconciled);
    }

    [Fact]
    public async Task UsageBillingDisabledWorkerDoesNotQueryOrDispatch()
    {
        var store = new RecordingStore();
        var dispatcher = new RecordingDispatcher();
        await using var provider = CreateProvider(store, dispatcher);
        var worker = CreateWorker(
            provider,
            EnabledOptions(),
            usageBillingEnabled: false);

        await worker.ProcessCycleAsync(CancellationToken.None);

        Assert.Equal(0, store.DispatchQueries);
        Assert.Equal(0, store.ReconciliationQueries);
        Assert.Equal(0, store.AbandonedQueries);
        Assert.Empty(dispatcher.Dispatched);
        Assert.Empty(dispatcher.Reconciled);
    }

    [Fact]
    public async Task CycleUsesBoundedQueriesAndDispatcherOnlyForEachCandidateType()
    {
        var dispatchId = Guid.NewGuid();
        var secondDispatchId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var abandonedId = Guid.NewGuid();
        var store = new RecordingStore
        {
            DispatchCandidates = [dispatchId, secondDispatchId],
            ReconciliationCandidates = [reconciliationId],
            AbandonedCandidates = [abandonedId]
        };
        var dispatcher = new RecordingDispatcher
        {
            DispatchResult = Outcome(dispatchId, CustomerBillingDeliveryDispatcherOutcome.RetryableFailure),
            ReconcileResult = Outcome(reconciliationId, CustomerBillingDeliveryDispatcherOutcome.Unsupported)
        };
        await using var provider = CreateProvider(store, dispatcher);
        var worker = CreateWorker(provider, EnabledOptions());

        await worker.ProcessCycleAsync(CancellationToken.None);

        Assert.Equal(1, store.DispatchQueries);
        Assert.Equal(25, store.LastDispatchLimit);
        Assert.Equal(TimeSpan.FromSeconds(300), store.LastRetryDelay);
        Assert.Equal(1, store.ReconciliationQueries);
        Assert.Equal(25, store.LastReconciliationLimit);
        Assert.Equal(TimeSpan.FromSeconds(600), store.LastReconciliationDelay);
        Assert.Equal(1, store.AbandonedQueries);
        Assert.Equal(25, store.LastAbandonedLimit);
        Assert.Equal([dispatchId, secondDispatchId], dispatcher.Dispatched);
        Assert.Equal([reconciliationId], dispatcher.Reconciled);
        Assert.Equal([(abandonedId, true)], store.Recovered);
        Assert.DoesNotContain(abandonedId, dispatcher.Dispatched);
        Assert.DoesNotContain(reconciliationId, dispatcher.Dispatched);
    }

    [Fact]
    public async Task ItemFailureDoesNotPreventLaterDispatchCandidate()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var store = new RecordingStore { DispatchCandidates = [firstId, secondId] };
        var dispatcher = new RecordingDispatcher { ThrowOnDispatchId = firstId };
        await using var provider = CreateProvider(store, dispatcher);
        var worker = CreateWorker(provider, EnabledOptions());

        await worker.ProcessCycleAsync(CancellationToken.None);

        Assert.Equal([firstId, secondId], dispatcher.Dispatched);
    }

    [Fact]
    public async Task CancellationStopsBeforeStartingNextDelivery()
    {
        using var cancellation = new CancellationTokenSource();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var store = new RecordingStore { DispatchCandidates = [firstId, secondId] };
        var dispatcher = new RecordingDispatcher { CancelOnDispatch = cancellation };
        await using var provider = CreateProvider(store, dispatcher);
        var worker = CreateWorker(provider, EnabledOptions());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => worker.ProcessCycleAsync(cancellation.Token));

        Assert.Equal([firstId], dispatcher.Dispatched);
        Assert.Empty(dispatcher.Reconciled);
    }

    [Fact]
    public void WorkerOptionsSkipWorkerSpecificValidationWhenDisabled()
    {
        var validator = new CustomerBillingDeliveryWorkerOptionsValidator(
            new ConfigurationBuilder().AddInMemoryCollection().Build());
        var options = new CustomerBillingDeliveryWorkerOptions
        {
            Enabled = false,
            PollIntervalSeconds = 1,
            BatchSize = 0
        };

        Assert.True(validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void WorkerOptionsFailClosedWhenUsageBillingIsDisabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var validator = new CustomerBillingDeliveryWorkerOptionsValidator(configuration);

        var result = validator.Validate(null, EnabledOptions());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("UsageBilling:Enabled", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkerOptionsRejectOutOfRangeValuesWhenEnabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Billing:UsageBilling:Enabled"] = "true"
            })
            .Build();
        var validator = new CustomerBillingDeliveryWorkerOptionsValidator(configuration);
        var options = EnabledOptions();
        options.PollIntervalSeconds = 4;

        Assert.False(validator.Validate(null, options).Succeeded);
    }

    private static Microsoft.Extensions.DependencyInjection.ServiceProvider CreateProvider(
        RecordingStore store,
        RecordingDispatcher dispatcher)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICustomerBillingDeliveryStore>(_ => store);
        services.AddScoped<ICustomerBillingDeliveryDispatcher>(_ => dispatcher);
        return services.BuildServiceProvider();
    }

    private static CustomerBillingDeliveryWorker CreateWorker(
        IServiceProvider provider,
        CustomerBillingDeliveryWorkerOptions options,
        bool usageBillingEnabled = true) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            Options.Create(new CustomerUsageBillingOptions { Enabled = usageBillingEnabled }),
            TimeProvider.System,
            NullLogger<CustomerBillingDeliveryWorker>.Instance);

    private static CustomerBillingDeliveryWorkerOptions EnabledOptions() => new()
    {
        Enabled = true,
        PollIntervalSeconds = 30,
        BatchSize = 25,
        ReconciliationBatchSize = 25,
        RetryDelaySeconds = 300,
        ReconciliationDelaySeconds = 600,
        DispatchingRecoveryAgeSeconds = 900
    };

    private static CustomerBillingDeliveryDispatcherResult Outcome(
        Guid deliveryId,
        CustomerBillingDeliveryDispatcherOutcome outcome) =>
        new(deliveryId, outcome, null, "test", null, null, null, 1);

    private sealed class RecordingDispatcher : ICustomerBillingDeliveryDispatcher
    {
        public List<Guid> Dispatched { get; } = [];
        public List<Guid> Reconciled { get; } = [];
        public Guid? ThrowOnDispatchId { get; init; }
        public CancellationTokenSource? CancelOnDispatch { get; init; }
        public CustomerBillingDeliveryDispatcherResult DispatchResult { get; init; } =
            Outcome(Guid.Empty, CustomerBillingDeliveryDispatcherOutcome.NotEligible);
        public CustomerBillingDeliveryDispatcherResult ReconcileResult { get; init; } =
            Outcome(Guid.Empty, CustomerBillingDeliveryDispatcherOutcome.Unsupported);

        public Task<CustomerBillingDeliveryDispatcherResult> DispatchAsync(
            Guid deliveryId,
            CancellationToken cancellationToken)
        {
            Dispatched.Add(deliveryId);
            if (ThrowOnDispatchId == deliveryId)
            {
                throw new InvalidOperationException("test item failure");
            }

            if (CancelOnDispatch is not null)
            {
                CancelOnDispatch.Cancel();
            }

            return Task.FromResult(DispatchResult with { DeliveryId = deliveryId });
        }

        public Task<CustomerBillingDeliveryDispatcherResult> ReconcileAsync(
            Guid deliveryId,
            CancellationToken cancellationToken)
        {
            Reconciled.Add(deliveryId);
            return Task.FromResult(ReconcileResult with { DeliveryId = deliveryId });
        }
    }

    private sealed class RecordingStore : ICustomerBillingDeliveryStore
    {
        public IReadOnlyList<Guid> DispatchCandidates { get; init; } = [];
        public IReadOnlyList<Guid> ReconciliationCandidates { get; init; } = [];
        public IReadOnlyList<Guid> AbandonedCandidates { get; init; } = [];
        public List<(Guid DeliveryId, bool Changed)> Recovered { get; } = [];
        public int DispatchQueries { get; private set; }
        public int ReconciliationQueries { get; private set; }
        public int AbandonedQueries { get; private set; }
        public int LastDispatchLimit { get; private set; }
        public int LastReconciliationLimit { get; private set; }
        public int LastAbandonedLimit { get; private set; }
        public TimeSpan LastRetryDelay { get; private set; }
        public TimeSpan LastReconciliationDelay { get; private set; }

        public Task<IReadOnlyList<Guid>> GetDispatchCandidatesAsync(
            int limit,
            DateTimeOffset now,
            TimeSpan retryDelay,
            CancellationToken cancellationToken)
        {
            DispatchQueries++;
            LastDispatchLimit = limit;
            LastRetryDelay = retryDelay;
            return Task.FromResult(DispatchCandidates);
        }

        public Task<IReadOnlyList<Guid>> GetReconciliationCandidatesAsync(
            int limit,
            DateTimeOffset now,
            TimeSpan reconciliationDelay,
            CancellationToken cancellationToken)
        {
            ReconciliationQueries++;
            LastReconciliationLimit = limit;
            LastReconciliationDelay = reconciliationDelay;
            return Task.FromResult(ReconciliationCandidates);
        }

        public Task<IReadOnlyList<Guid>> GetAbandonedDispatchCandidatesAsync(
            int limit,
            DateTimeOffset cutoff,
            CancellationToken cancellationToken)
        {
            AbandonedQueries++;
            LastAbandonedLimit = limit;
            return Task.FromResult(AbandonedCandidates);
        }

        public Task<bool> MarkAbandonedDispatchIndeterminateAsync(
            Guid deliveryId,
            DateTimeOffset cutoff,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken)
        {
            Recovered.Add((deliveryId, true));
            return Task.FromResult(true);
        }

        public Task<bool> RecordReconciliationAttemptAsync(
            Guid deliveryId,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<CustomerBillableUsageOperation?> GetBillingOperationAsync(
            Guid billingOperationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CustomerBillingDelivery> PrepareAsync(Guid billingOperationId, DateTimeOffset preparedAtUtc) =>
            throw new NotSupportedException();

        public Task<CustomerBillingDelivery?> StageAsync(
            CustomerBillableUsageOperation operation,
            DateTimeOffset preparedAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CustomerBillingDelivery?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CustomerBillingDelivery?> GetByBillingOperationAsync(
            Guid billingOperationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryStartDispatchAsync(Guid deliveryId, DateTimeOffset occurredAtUtc) =>
            throw new NotSupportedException();

        public Task<bool> RecordDeliveredAsync(
            Guid deliveryId,
            string? externalObjectId,
            string? externalRequestId,
            DateTimeOffset occurredAtUtc) =>
            throw new NotSupportedException();

        public Task<bool> RecordFailedAsync(
            Guid deliveryId,
            CustomerBillingDeliveryFailureCategory failureCategory,
            string failureCode,
            string? externalRequestId,
            DateTimeOffset occurredAtUtc) =>
            throw new NotSupportedException();

        public Task<bool> RecordIndeterminateAsync(
            Guid deliveryId,
            string? externalObjectId,
            string? externalRequestId,
            string? failureCode,
            DateTimeOffset occurredAtUtc) =>
            throw new NotSupportedException();

        public Task<bool> RecordReconciledDeliveredAsync(
            Guid deliveryId,
            string? externalObjectId,
            string? externalRequestId,
            DateTimeOffset occurredAtUtc) =>
            throw new NotSupportedException();

        public Task<bool> RecordReconciledRejectedAsync(
            Guid deliveryId,
            string failureCode,
            string? externalRequestId,
            DateTimeOffset occurredAtUtc) =>
            throw new NotSupportedException();
    }
}
