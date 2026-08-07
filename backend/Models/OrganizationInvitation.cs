namespace StayFlow.Api.Models;

public sealed class OrganizationInvitation : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid InvitedByUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string Role { get; set; } = OrganizationRole.ReadOnly.ToStorageValue();
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public DateTimeOffset? RejectedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset? LastSentAtUtc { get; set; }
    public int SendCount { get; set; }

    public Company Company { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
    public User? AcceptedByUser { get; set; }
}