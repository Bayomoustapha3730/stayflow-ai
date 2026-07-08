using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Guests;

namespace StayFlow.Api.Services;

public interface IGuestService
{
    Task<ApiResponse<PagedResult<GuestSummaryDto>>> GetAsync(GuestQueryParameters query, CancellationToken cancellationToken);
    Task<ApiResponse<GuestDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ApiResponse<GuestDto>> CreateAsync(CreateGuestRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<GuestDto>> UpdateAsync(Guid id, UpdateGuestRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
