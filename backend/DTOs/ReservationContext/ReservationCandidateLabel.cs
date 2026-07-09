namespace StayFlow.Api.DTOs.ReservationContext;

public sealed class ReservationCandidateLabel
{
    public string PropertyName { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public DateOnly CheckInDate { get; init; }
}
