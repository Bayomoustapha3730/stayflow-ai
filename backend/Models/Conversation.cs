namespace StayFlow.Api.Models;

public sealed class Conversation : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid GuestId { get; set; }
    public string Channel { get; set; } = "WhatsApp";
    public string? ExternalThreadId { get; set; }
    public string Status { get; set; } = "Open";
    public Guid? ReservationId { get; set; }
    public DateTimeOffset? ReservationContextBoundAt { get; set; }
    public string? ReservationContextResolutionMethod { get; set; }

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Guest Guest { get; set; } = null!;
    public Reservation? Reservation { get; set; }
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
}
