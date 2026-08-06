namespace StayFlow.Api.DTOs.Companies;

public sealed class UpdateCompanyRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Slug { get; init; }
    public string? Status { get; init; }
    public string? LegalName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string CountryCode { get; init; } = "KE";
    public string TimeZone { get; init; } = "Africa/Nairobi";
    public string? BrandingLogoUrl { get; init; }
    public string? BrandingPrimaryColor { get; init; }
    public string? OnboardingState { get; init; }
    public bool IsActive { get; init; } = true;
}
