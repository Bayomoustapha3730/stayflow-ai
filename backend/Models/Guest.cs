namespace StayFlow.Api.Models;

public sealed class Guest : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public string CountryCode { get; set; } = "KE";
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<Reservation> PrimaryReservations { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
