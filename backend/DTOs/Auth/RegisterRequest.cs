namespace StayFlow.Api.DTOs.Auth;

public sealed class RegisterRequest
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? InvitationToken { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public string? OrganizationSlug { get; init; }
    public string CountryCode { get; init; } = "KE";
    public string TimeZone { get; init; } = "Africa/Nairobi";
}