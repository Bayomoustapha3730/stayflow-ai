namespace StayFlow.Api.DTOs.Organizations;

public sealed class UpdateOrganizationMemberRoleRequest
{
    public string Role { get; init; } = string.Empty;
}