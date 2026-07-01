namespace StayFlow.Api.DTOs.Properties;

public sealed class PropertyValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = [];
}
