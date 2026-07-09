namespace StayFlow.Api.Services;

public sealed class ReservationContextOptions
{
    public const string SectionName = "ReservationContext";
    public int PreArrivalWindowDays { get; init; } = 7;
}
