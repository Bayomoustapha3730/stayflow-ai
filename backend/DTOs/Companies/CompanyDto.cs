namespace StayFlow.Api.DTOs.Companies;

public sealed class CompanyDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? OwnerUserId { get; init; }
    public string? LegalName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string TimeZone { get; init; } = string.Empty;
    public string? BrandingLogoUrl { get; init; }
    public string? BrandingPrimaryColor { get; init; }
    public string? OnboardingState { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
