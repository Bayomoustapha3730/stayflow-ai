using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface IReservationLifecycleService
{
    ReservationLifecycleContext GetContext(Reservation reservation, Property property);
}