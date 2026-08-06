namespace StayFlow.Api.DTOs.Organizations;

public sealed class OrganizationDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? OwnerUserId { get; init; }
    public string? BrandingLogoUrl { get; init; }
    public string? BrandingPrimaryColor { get; init; }
    public string? OnboardingState { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}