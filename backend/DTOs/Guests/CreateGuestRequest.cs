namespace StayFlow.Api.DTOs.Guests;

public sealed class CreateGuestRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string PreferredLanguage { get; init; } = "en";
    public string CountryCode { get; init; } = "KE";
    public string? Notes { get; init; }
}
