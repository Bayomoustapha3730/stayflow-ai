using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Auth;

namespace StayFlow.Api.Services;

public interface IRoleService
{
    Task<ApiResponse<IReadOnlyCollection<RoleDto>>> GetRolesAsync(CancellationToken cancellationToken);
    Task<ApiResponse<RoleDto>> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<RoleDto>> AssignPermissionAsync(Guid roleId, AssignPermissionRequest request, CancellationToken cancellationToken);
}
