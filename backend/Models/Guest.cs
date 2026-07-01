namespace StayFlow.Api.Models;

public sealed class Guest : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
