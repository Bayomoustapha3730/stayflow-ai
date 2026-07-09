using StayFlow.Api.DTOs.ReservationContext;

namespace StayFlow.Api.Services;

public interface IReservationContextResolver
{
    Task<ReservationContextResolutionResult> ResolveAsync(ReservationContextRequest request, CancellationToken cancellationToken);
}
