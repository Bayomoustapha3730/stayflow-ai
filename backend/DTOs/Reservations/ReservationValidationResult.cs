namespace StayFlow.Api.DTOs.Reservations;

public sealed class ReservationValidationResult
{
    public List<string> Errors { get; } = [];
    public bool IsValid => Errors.Count == 0;
}
