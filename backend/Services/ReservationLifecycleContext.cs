namespace StayFlow.Api.Services;

public sealed class ReservationLifecycleContext
{
    public Guid ReservationId { get; init; }
    public Guid CompanyId { get; init; }
    public Guid PropertyId { get; init; }
    public Guid GuestId { get; init; }
    public ReservationLifecycleStage LifecycleStage { get; init; }
    public DateOnly CheckInLocal { get; init; }
    public DateOnly CheckOutLocal { get; init; }
    public int DaysUntilCheckIn { get; init; }
    public int DaysUntilCheckOut { get; init; }
    public bool IsCurrentlyInStay { get; init; }
    public string PropertyTimeZone { get; init; } = string.Empty;
    public DateTimeOffset CurrentLocalDateTime { get; init; }
    public int PreArrivalWindowDays { get; init; }
}