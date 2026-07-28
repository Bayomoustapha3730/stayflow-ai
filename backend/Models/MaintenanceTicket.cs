namespace StayFlow.Api.Models;

public sealed class MaintenanceTicket : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid ConversationId { get; set; }
    public MaintenanceCategory Category { get; set; } = MaintenanceCategory.Other;
    public string DescriptionSummary { get; set; } = string.Empty;
    public MaintenanceUrgency Urgency { get; set; } = MaintenanceUrgency.Routine;
    public string? Location { get; set; }
    public MaintenanceTicketStatus Status { get; set; } = MaintenanceTicketStatus.Open;
    public Guid? AssignedUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Reservation? Reservation { get; set; }
    public Conversation Conversation { get; set; } = null!;
}
