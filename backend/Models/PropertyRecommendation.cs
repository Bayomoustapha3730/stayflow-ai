namespace StayFlow.Api.Models;

public sealed class PropertyRecommendation : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public Property Property { get; set; } = null!;
}
