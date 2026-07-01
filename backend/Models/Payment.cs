namespace StayFlow.Api.Models;

public sealed class Payment : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid GuestId { get; set; }
    public Guid? ServiceRequestId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KES";
    public string? ExternalReference { get; set; }
    public string Status { get; set; } = "Pending";

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Guest Guest { get; set; } = null!;
    public ServiceRequest? ServiceRequest { get; set; }
}
