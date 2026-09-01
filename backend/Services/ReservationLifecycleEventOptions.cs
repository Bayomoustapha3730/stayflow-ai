namespace StayFlow.Api.Services;

public sealed class ReservationLifecycleEventOptions
{
    public const string SectionName = "ReservationLifecycleEvents";
    public TimeOnly DefaultLocalTriggerTime { get; init; } = new TimeOnly(9, 0);
}
