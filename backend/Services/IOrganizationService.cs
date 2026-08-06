using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Organizations;

namespace StayFlow.Api.Services;

public interface IOrganizationService
{
    Task<ApiResponse<OrganizationDto>> GetCurrentAsync(CancellationToken cancellationToken);
    Task<ApiResponse<OrganizationDto>> UpdateCurrentAsync(UpdateCurrentOrganizationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<IReadOnlyCollection<OrganizationMemberDto>>> GetCurrentMembersAsync(CancellationToken cancellationToken);
    Task<ApiResponse<OrganizationMemberDto>> UpdateMemberRoleAsync(Guid memberUserId, UpdateOrganizationMemberRoleRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<object>> RemoveMemberAsync(Guid memberUserId, CancellationToken cancellationToken);
}