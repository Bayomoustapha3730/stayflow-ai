namespace StayFlow.Api.Models;

public sealed class OrganizationMember : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = OrganizationRole.ReadOnly.ToStorageValue();
    public string Status { get; set; } = OrganizationMemberStatus.Active.ToStorageValue();
    public DateTimeOffset JoinedAt { get; set; }
    public Guid? InvitedByUserId { get; set; }

    public Company Company { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? InvitedByUser { get; set; }
}