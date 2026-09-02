namespace StayFlow.Api.Services;

public sealed class GuestJourneyDeliveryOptions
{
    public const string SectionName = "GuestJourneyDelivery";

    public bool WorkerEnabled { get; init; }
    public int PollingIntervalSeconds { get; init; } = 30;
    public int BatchSize { get; init; } = 25;
    public int ProcessingLeaseTimeoutMinutes { get; init; } = 15;
    public int RetryDelayMinutes { get; init; } = 5;
    public int MaxAttempts { get; init; } = 5;
}
