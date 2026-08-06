namespace StayFlow.Api.DTOs.Organizations;

public sealed class UpdateCurrentOrganizationRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Slug { get; init; }
    public string? Status { get; init; }
    public string? BrandingLogoUrl { get; init; }
    public string? BrandingPrimaryColor { get; init; }
    public string? OnboardingState { get; init; }
}