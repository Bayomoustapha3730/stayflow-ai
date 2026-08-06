namespace StayFlow.Api.DTOs.Onboarding;

public sealed class CompleteOnboardingOrganizationStepRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Slug { get; init; }
    public string? BrandingLogoUrl { get; init; }
    public string? BrandingPrimaryColor { get; init; }
}

public sealed class CompleteOnboardingPlanStepRequest
{
    public string PlanName { get; init; } = string.Empty;
}

public sealed class CompleteOnboardingPropertyStepRequest
{
    public Guid PropertyId { get; init; }
}

public sealed class CompleteOnboardingTeamStepRequest
{
    public int? InvitedCount { get; init; }
}