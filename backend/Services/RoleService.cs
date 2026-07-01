using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class RoleService(ApplicationDbContext dbContext) : IRoleService
{
    public async Task<ApiResponse<IReadOnlyCollection<RoleDto>>> GetRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Include(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .Where(role => role.IsActive)
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

        return ApiResponse<IReadOnlyCollection<RoleDto>>.Ok(roles.Select(MapRole).ToList());
    }

    public async Task<ApiResponse<RoleDto>> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<RoleDto>.Fail("Role name is required.");
        }

        var roleName = request.Name.Trim();
        var exists = await dbContext.Roles.AnyAsync(role => role.Name == roleName, cancellationToken);
        if (exists)
        {
            return ApiResponse<RoleDto>.Fail("Role already exists.");
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = roleName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true
        };

        await dbContext.Roles.AddAsync(role, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<RoleDto>.Ok(MapRole(role), "Role created successfully.");
    }

    public async Task<ApiResponse<RoleDto>> AssignPermissionAsync(
        Guid roleId,
        AssignPermissionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PermissionName))
        {
            return ApiResponse<RoleDto>.Fail("Permission name is required.");
        }

        var role = await dbContext.Roles
            .Include(existingRole => existingRole.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .FirstOrDefaultAsync(existingRole => existingRole.Id == roleId && existingRole.IsActive, cancellationToken);

        if (role is null)
        {
            return ApiResponse<RoleDto>.Fail("Role was not found.");
        }

        var permissionName = request.PermissionName.Trim();
        var permission = await dbContext.Permissions
            .FirstOrDefaultAsync(existingPermission => existingPermission.Name == permissionName, cancellationToken);

        if (permission is null)
        {
            permission = new Permission
            {
                Id = Guid.NewGuid(),
                Name = permissionName,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
            };
            await dbContext.Permissions.AddAsync(permission, cancellationToken);
        }

        if (role.RolePermissions.All(rolePermission => rolePermission.PermissionId != permission.Id))
        {
            role.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id,
                Permission = permission,
                Role = role
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<RoleDto>.Ok(MapRole(role), "Permission assigned successfully.");
    }

    private static RoleDto MapRole(Role role)
    {
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            Permissions = role.RolePermissions
                .Select(rolePermission => rolePermission.Permission.Name)
                .Distinct()
                .OrderBy(permission => permission)
                .ToList()
        };
    }
}
