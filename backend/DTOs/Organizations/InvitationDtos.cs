namespace StayFlow.Api.DTOs.Organizations;

public sealed class CreateOrganizationInvitationRequest
{
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int? ExpiresInHours { get; init; }
}

public sealed class AcceptOrganizationInvitationRequest
{
    public string Token { get; init; } = string.Empty;
}

public sealed class OrganizationInvitationDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public DateTimeOffset? AcceptedAtUtc { get; init; }
    public DateTimeOffset? RevokedAtUtc { get; init; }
    public DateTimeOffset? LastSentAtUtc { get; init; }
    public int SendCount { get; init; }
}

public sealed class CreatedOrganizationInvitationDto
{
    public OrganizationInvitationDto Invitation { get; init; } = new();
    public string InvitationToken { get; init; } = string.Empty;
    public string InvitationLink { get; init; } = string.Empty;
}

public sealed class ResentOrganizationInvitationDto
{
    public OrganizationInvitationDto Invitation { get; init; } = new();
    public string InvitationToken { get; init; } = string.Empty;
    public string InvitationLink { get; init; } = string.Empty;
}