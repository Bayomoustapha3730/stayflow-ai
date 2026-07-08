namespace StayFlow.Api.DTOs.Guests;

public sealed class GuestSummaryDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
