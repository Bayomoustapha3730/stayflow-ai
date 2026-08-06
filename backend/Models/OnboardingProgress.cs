namespace StayFlow.Api.Models;

public sealed class OnboardingProgress : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string CurrentStep { get; set; } = OnboardingStep.AccountCreated.ToStorageValue();
    public string? SelectedPlanName { get; set; }
    public Guid? FirstPropertyId { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset LastUpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Company Company { get; set; } = null!;
    public User User { get; set; } = null!;
}