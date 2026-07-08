using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Reservations;

namespace StayFlow.Api.Services;

public interface IReservationService
{
    Task<ApiResponse<PagedResult<ReservationSummaryDto>>> GetAsync(ReservationQueryParameters query, CancellationToken cancellationToken);
    Task<ApiResponse<ReservationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ApiResponse<ReservationDto>> CreateAsync(CreateReservationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ReservationDto>> UpdateAsync(Guid id, UpdateReservationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<ReservationDto>> TransitionStatusAsync(Guid id, TransitionReservationStatusRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
