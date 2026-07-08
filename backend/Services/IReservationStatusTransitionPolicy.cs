using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface IReservationStatusTransitionPolicy
{
    bool CanTransition(ReservationStatus currentStatus, ReservationStatus targetStatus);
}
