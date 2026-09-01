namespace StayFlow.Api.Services;

public sealed class ReservationLifecycleEventOptions
{
    public const string SectionName = "ReservationLifecycleEvents";

    public bool WorkerEnabled { get; init; }
    public int PollingIntervalSeconds { get; init; } = 60;
    public int GenerationBatchSize { get; init; } = 100;
    public int ProcessingBatchSize { get; init; } = 25;
    public int GenerationLookbackDays { get; init; } = 2;
    public int GenerationHorizonDays { get; init; } = 30;
    public int ProcessingLeaseTimeoutMinutes { get; init; } = 15;
    public int RetryDelayMinutes { get; init; } = 5;
    public int MaxAttempts { get; init; } = 3;
    public TimeOnly DefaultLocalTriggerTime { get; init; } = new TimeOnly(9, 0);
}
