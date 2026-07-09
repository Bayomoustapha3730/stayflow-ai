namespace StayFlow.Api.DTOs.AIContext;

public sealed class AIPropertyContext
{
    public string DisplayName { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string TimeZone { get; init; } = string.Empty;
    public string? Description { get; init; }
}
