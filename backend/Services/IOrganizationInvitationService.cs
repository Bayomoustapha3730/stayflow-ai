using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Organizations;

namespace StayFlow.Api.Services;

public interface IOrganizationInvitationService
{
    Task<ApiResponse<CreatedOrganizationInvitationDto>> CreateAsync(CreateOrganizationInvitationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<IReadOnlyCollection<OrganizationInvitationDto>>> ListAsync(CancellationToken cancellationToken);
    Task<ApiResponse<object>> RevokeAsync(Guid invitationId, CancellationToken cancellationToken);
    Task<ApiResponse<ResentOrganizationInvitationDto>> ResendAsync(Guid invitationId, CancellationToken cancellationToken);
    Task<ApiResponse<object>> AcceptAsync(AcceptOrganizationInvitationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<object>> RejectAsync(RejectOrganizationInvitationRequest request, CancellationToken cancellationToken);
}