namespace StayFlow.Api.DTOs.Onboarding;

public sealed class OnboardingStatusDto
{
    public Guid CompanyId { get; init; }
    public Guid UserId { get; init; }
    public string CurrentStep { get; init; } = string.Empty;
    public string CurrentStepState { get; init; } = string.Empty;
    public IReadOnlyCollection<string> CompletedSteps { get; init; } = [];
    public IReadOnlyCollection<string> RemainingSteps { get; init; } = [];
    public IReadOnlyCollection<string> SkippedSteps { get; init; } = [];
    public IReadOnlyCollection<OnboardingBlockerDto> Blockers { get; init; } = [];
    public IReadOnlyCollection<OnboardingChecklistItemDto> Checklist { get; init; } = [];
    public int PercentComplete { get; init; }
    public string? NextRecommendedAction { get; init; }
    public IReadOnlyCollection<OnboardingSafeLinkDto> SafeLinks { get; init; } = [];
    public DateTimeOffset StartedAtUtc { get; init; }
    public string? SelectedPlanName { get; init; }
    public Guid? FirstPropertyId { get; init; }
    public bool IsCompleted { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public Guid? CompletedByUserId { get; init; }
    public DateTimeOffset LastUpdatedAtUtc { get; init; }
    public int Version { get; init; }
}

public sealed class OnboardingBlockerDto
{
    public string Step { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class OnboardingChecklistItemDto
{
    public string Key { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool Optional { get; init; }
    public string Recommendation { get; init; } = string.Empty;
}

public sealed class OnboardingSafeLinkDto
{
    public string Rel { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
}

public sealed class OnboardingActionResponse<T>
{
    public OnboardingStatusDto Status { get; init; } = new();
    public T? Result { get; init; }
}