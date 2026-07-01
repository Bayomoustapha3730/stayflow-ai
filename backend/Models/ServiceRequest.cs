namespace StayFlow.Api.Models;

public sealed class ServiceRequest : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid GuestId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? ServiceProviderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Open";

    public Company Company { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public Guest Guest { get; set; } = null!;
    public Conversation? Conversation { get; set; }
    public ServiceProvider? ServiceProvider { get; set; }
    public ICollection<Payment> Payments { get; set; } = [];
}
