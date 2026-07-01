namespace StayFlow.Api.Models;

public sealed class ServiceProvider : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
}
