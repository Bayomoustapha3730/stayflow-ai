namespace StayFlow.Api.DTOs.Auth;

public sealed class AuthOrganizationSummaryDto
{
    public Guid CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string MembershipStatus { get; init; } = string.Empty;
    public bool IsActiveOrganization { get; init; }
    public string OrganizationStatus { get; init; } = string.Empty;
    public string? OnboardingState { get; init; }
    public int PropertyCount { get; init; }
    public string? PlanName { get; init; }
    public string? SubscriptionStatus { get; init; }
}