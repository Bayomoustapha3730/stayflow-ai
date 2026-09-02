namespace StayFlow.Api.Models;

public enum GuestJourneyMessageStatus
{
    Pending,
    Processing,
    Accepted,
    Delivered,
    Failed,
    Suppressed,
    Blocked
}