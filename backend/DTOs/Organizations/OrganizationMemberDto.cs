namespace StayFlow.Api.DTOs.Organizations;

public sealed class OrganizationMemberDto
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset JoinedAt { get; init; }
    public Guid? InvitedByUserId { get; init; }
}