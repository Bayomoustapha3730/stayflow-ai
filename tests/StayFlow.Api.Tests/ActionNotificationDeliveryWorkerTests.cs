using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Extensions;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Tests;

public sealed class ActionNotificationDeliveryWorkerTests
{
    [Fact]
    public async Task DisabledWorker_PerformsNoWork()
    {
        var state = new ProcessorState();
        await using var provider = BuildProvider(state);
        var worker = CreateWorker(provider, workerEnabled: false, pollingIntervalSeconds: 1);

        await worker.StartAsync(CancellationToken.None);

        Assert.Equal(0, state.CallCount);
        Assert.Equal(0, state.InstanceCount);
    }

    [Fact]
    public async Task EnabledWorker_ProcessesThenWaitsForPollingInterval()
    {
        var state = new ProcessorState();
        await using var provider = BuildProvider(state);
        var worker = CreateWorker(provider, workerEnabled: true, pollingIntervalSeconds: 1);

        await worker.StartAsync(CancellationToken.None);
        await state.FirstCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, state.CallCount);
    }

    [Fact]
    public async Task EnabledWorker_CreatesFreshScopeForEachIteration()
    {
        var state = new ProcessorState();
        await using var provider = BuildProvider(state);
        var worker = CreateWorker(provider, workerEnabled: true, pollingIntervalSeconds: 1);

        await worker.StartAsync(CancellationToken.None);
        await state.SecondCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(state.CallCount >= 2);
        Assert.True(state.InstanceCount >= 2);
    }

    [Fact]
    public async Task ProcessorException_IsolatedAndNextPollContinues()
    {
        var state = new ProcessorState { ThrowOnFirstCall = true };
        await using var provider = BuildProvider(state);
        var worker = CreateWorker(provider, workerEnabled: true, pollingIntervalSeconds: 1);

        await worker.StartAsync(CancellationToken.None);
        await state.SecondCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(state.CallCount >= 2);
    }

    [Fact]
    public async Task Cancellation_StopsActiveProcessorAndWorker()
    {
        var processor = new CancellableProcessor();
        var services = new ServiceCollection();
        services.AddScoped<IActionNotificationDeliveryProcessor>(_ => processor);
        await using var provider = services.BuildServiceProvider();
        var worker = CreateWorker(provider, workerEnabled: true, pollingIntervalSeconds: 60);

        await worker.StartAsync(CancellationToken.None);
        await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(processor.CancellationObserved.Task.IsCompletedSuccessfully);
    }

    private static ServiceProvider BuildProvider(ProcessorState state)
    {
        var services = new ServiceCollection();
        services.AddScoped<IActionNotificationDeliveryProcessor>(_ => new RecordingProcessor(state));
        return services.BuildServiceProvider();
    }

    private static ActionNotificationDeliveryWorker CreateWorker(
        ServiceProvider provider,
        bool workerEnabled,
        int pollingIntervalSeconds)
        => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ActionNotificationDeliveryOptions
            {
                WorkerEnabled = workerEnabled,
                PollingIntervalSeconds = pollingIntervalSeconds
            }),
            NullLogger<ActionNotificationDeliveryWorker>.Instance);

    private sealed class ProcessorState
    {
        private int callCount;
        private int instanceCount;

        public bool ThrowOnFirstCall { get; init; }
        public int CallCount => Volatile.Read(ref callCount);
        public int InstanceCount => Volatile.Read(ref instanceCount);
        public TaskCompletionSource FirstCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void RecordInstance() => Interlocked.Increment(ref instanceCount);

        public int RecordCall()
        {
            var current = Interlocked.Increment(ref callCount);
            FirstCall.TrySetResult();
            if (current >= 2)
            {
                SecondCall.TrySetResult();
            }

            return current;
        }
    }

    private sealed class RecordingProcessor : IActionNotificationDeliveryProcessor
    {
        private readonly ProcessorState state;

        public RecordingProcessor(ProcessorState state)
        {
            this.state = state;
            state.RecordInstance();
        }

        public Task<ActionNotificationDeliveryResult> ProcessDueAsync(CancellationToken cancellationToken)
        {
            var call = state.RecordCall();
            if (state.ThrowOnFirstCall && call == 1)
            {
                throw new InvalidOperationException("simulated processor failure");
            }

            return Task.FromResult(new ActionNotificationDeliveryResult(0, 0, 0, 0));
        }
    }

    private sealed class CancellableProcessor : IActionNotificationDeliveryProcessor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ActionNotificationDeliveryResult> ProcessDueAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    CancellationObserved.TrySetResult();
                }
            }

            return new ActionNotificationDeliveryResult(0, 0, 0, 0);
        }
    }
}

public sealed class ActionNotificationDeliveryOptionsTests
{
    [Fact]
    public void BaseConfiguration_ExplicitlyDisablesWorkerWithProductionDefaults()
    {
        var options = LoadJsonOptions(includeDevelopment: false);

        Assert.False(options.WorkerEnabled);
        Assert.Equal(30, options.PollingIntervalSeconds);
        Assert.Equal(25, options.BatchSize);
        Assert.Equal(5, options.MaxAttempts);
        Assert.Equal(5, options.RetryDelayMinutes);
    }

    [Fact]
    public void DevelopmentConfiguration_ExplicitlyEnablesWorker()
    {
        var options = LoadJsonOptions(includeDevelopment: true);

        Assert.True(options.WorkerEnabled);
        Assert.Equal(5, options.PollingIntervalSeconds);
        Assert.Equal(25, options.BatchSize);
        Assert.Equal(5, options.MaxAttempts);
        Assert.Equal(1, options.RetryDelayMinutes);
    }

    [Theory]
    [InlineData("PollingIntervalSeconds", "0")]
    [InlineData("BatchSize", "0")]
    [InlineData("MaxAttempts", "0")]
    [InlineData("RetryDelayMinutes", "-1")]
    public void InvalidConfiguration_FailsOptionsValidation(string key, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ActionNotificationDelivery:{key}"] = value
            })
            .Build();
        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ActionNotificationDeliveryOptions>>().Value);
    }

    [Fact]
    public void ZeroRetryDelay_IsValid()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ActionNotificationDelivery:RetryDelayMinutes"] = "0"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ActionNotificationDeliveryOptions>>().Value;

        Assert.Equal(0, options.RetryDelayMinutes);
    }

    private static ActionNotificationDeliveryOptions LoadJsonOptions(bool includeDevelopment)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false);

        if (includeDevelopment)
        {
            builder.AddJsonFile("appsettings.Development.json", optional: false);
        }

        return builder.Build()
            .GetSection(ActionNotificationDeliveryOptions.SectionName)
            .Get<ActionNotificationDeliveryOptions>()!;
    }
}