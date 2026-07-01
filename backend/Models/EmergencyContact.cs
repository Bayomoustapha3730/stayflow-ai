namespace StayFlow.Api.Models;

public sealed class EmergencyContact : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Property Property { get; set; } = null!;
}
