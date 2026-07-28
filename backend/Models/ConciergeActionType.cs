namespace StayFlow.Api.Models;

public enum ConciergeActionType
{
    None = 0,
    RequestEarlyCheckIn = 1,
    RequestLateCheckout = 2,
    CreateMaintenanceTicket = 3,
    RequestHousekeeping = 4,
    RequestExtraItem = 5,
    RequestParking = 6,
    NotifyHost = 7
}
