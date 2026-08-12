namespace StayFlow.Api.DTOs.Auth;

public sealed class CreateOrganizationRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Slug { get; init; }
    public string SupportContactEmail { get; init; } = string.Empty;
    public string CountryCode { get; init; } = "KE";
    public string TimeZone { get; init; } = "Africa/Nairobi";
}