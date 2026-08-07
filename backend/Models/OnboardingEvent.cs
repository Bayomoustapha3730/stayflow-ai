namespace StayFlow.Api.Models;

public sealed class OnboardingEvent : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string? Step { get; set; }
    public string? State { get; set; }
    public string MetadataJson { get; set; } = "{}";

    public Company Company { get; set; } = null!;
    public User User { get; set; } = null!;
}
