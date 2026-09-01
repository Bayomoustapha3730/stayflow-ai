using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface IReservationLifecycleEventHandler
{
    Task HandleAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken);
}