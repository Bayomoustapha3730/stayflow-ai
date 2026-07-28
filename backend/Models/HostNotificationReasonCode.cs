namespace StayFlow.Api.Models;

public enum HostNotificationReasonCode
{
    GuestArrivalUpdate = 0,
    EarlyCheckInRequest = 1,
    LateCheckoutRequest = 2,
    MaintenanceIssue = 3,
    HousekeepingRequest = 4,
    ExtraItemRequest = 5,
    ParkingRequest = 6,
    GuestNeedsAssistance = 7,
    SafetyConcern = 8
}
