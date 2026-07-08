namespace StayFlow.Api.Models;

public enum ReservationStatus
{
    Draft,
    PendingConfirmation,
    Confirmed,
    PreArrival,
    ReadyForCheckIn,
    CheckedIn,
    ActiveStay,
    CheckOutPending,
    CheckedOut,
    PostStay,
    Completed,
    Cancelled,
    NoShow
}
