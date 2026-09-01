namespace StayFlow.Api.DTOs.AIContext;

public sealed class AIReservationContext
{
    public string Status { get; init; } = string.Empty;
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public string CurrentStayPhase { get; init; } = string.Empty;
    public string LifecycleStage { get; init; } = string.Empty;
    public DateOnly CheckInLocal { get; init; }
    public DateOnly CheckOutLocal { get; init; }
    public int DaysUntilCheckIn { get; init; }
    public int DaysUntilCheckOut { get; init; }
    public bool IsCurrentlyInStay { get; init; }
    public string PropertyTimeZone { get; init; } = string.Empty;
    public int Adults { get; init; }
    public int Children { get; init; }
    public string? SpecialRequests { get; init; }
}
