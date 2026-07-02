namespace StayFlow.Api.Models;

public sealed class PropertyAmenity : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Property Property { get; set; } = null!;
}
