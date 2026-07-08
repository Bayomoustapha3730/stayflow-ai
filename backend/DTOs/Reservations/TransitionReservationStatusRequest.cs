namespace StayFlow.Api.DTOs.Reservations;

public sealed class TransitionReservationStatusRequest
{
    public string TargetStatus { get; init; } = string.Empty;
}
