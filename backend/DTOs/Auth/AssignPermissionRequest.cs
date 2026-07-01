namespace StayFlow.Api.DTOs.Auth;

public sealed class AssignPermissionRequest
{
    public string PermissionName { get; init; } = string.Empty;
    public string? Description { get; init; }
}
