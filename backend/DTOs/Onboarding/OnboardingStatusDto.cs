namespace StayFlow.Api.DTOs.Onboarding;

public sealed class OnboardingStatusDto
{
    public Guid CompanyId { get; init; }
    public Guid UserId { get; init; }
    public string CurrentStep { get; init; } = string.Empty;
    public string? SelectedPlanName { get; init; }
    public Guid? FirstPropertyId { get; init; }
    public bool IsCompleted { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset LastUpdatedAtUtc { get; init; }
}