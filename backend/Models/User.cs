namespace StayFlow.Api.Models;

public sealed class User : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
}
