using StayFlow.Api.DTOs.ReservationContext;

namespace StayFlow.Api.Models;

public sealed class Conversation : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid GuestId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? PropertyId { get; set; }
    public GuestChannel Channel { get; set; } = GuestChannel.Web;
    public string? ChannelIdentity { get; set; }
    public string? ExternalThreadId { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;
    public string? Subject { get; set; }
    public Guid? AssignedUserId { get; set; }
    public bool HumanTakeoverEnabled { get; set; }
    public string? EscalationReason { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset? ReservationContextBoundAt { get; set; }
    public string? ReservationContextResolutionMethod { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public Company Company { get; set; } = null!;
    public Property? Property { get; set; }
    public Guest Guest { get; set; } = null!;
    public Reservation? Reservation { get; set; }
    public User? AssignedUser { get; set; }
    public ICollection<ConversationMessage> Messages { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
}
