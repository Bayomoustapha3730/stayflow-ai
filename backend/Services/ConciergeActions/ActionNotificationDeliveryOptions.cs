namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ActionNotificationDeliveryOptions
{
    public const string SectionName = "ActionNotificationDelivery";

    public bool WorkerEnabled { get; init; }
    public int PollingIntervalSeconds { get; init; } = 30;
    public int BatchSize { get; init; } = 25;
    public int MaxAttempts { get; init; } = 5;
    public int RetryDelayMinutes { get; init; } = 5;
}
