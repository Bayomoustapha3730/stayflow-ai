namespace StayFlow.Api.Models;

public sealed class HouseRule : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Property Property { get; set; } = null!;
}
