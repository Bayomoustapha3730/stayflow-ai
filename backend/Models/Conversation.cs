namespace StayFlow.Api.Models;

public sealed class Conversation : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid GuestId { get; set; }
    public string Channel { get; set; } = "WhatsApp";
    public string? ExternalThreadId { get; set; }
    public string Status { get; set; } = "Open";

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Guest Guest { get; set; } = null!;
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
}
