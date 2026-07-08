namespace StayFlow.Api.DTOs.Guests;

public sealed class GuestValidationResult
{
    public List<string> Errors { get; } = [];
    public bool IsValid => Errors.Count == 0;
}
