namespace StayFlow.Api.Models;

public sealed class OnboardingProgress : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string CurrentStep { get; set; } = OnboardingStep.Welcome.ToStorageValue();
    public string CompletedStepsCsv { get; set; } = string.Empty;
    public string SkippedStepsCsv { get; set; } = string.Empty;
    public string? SelectedPlanName { get; set; }
    public Guid? FirstPropertyId { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTimeOffset LastUpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Version { get; set; }

    public Company Company { get; set; } = null!;
    public User User { get; set; } = null!;
}