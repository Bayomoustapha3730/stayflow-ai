namespace StayFlow.Api.Services;

public enum ReservationLifecycleStage
{
    NotConfirmed,
    FutureConfirmed,
    PreArrival,
    ArrivingToday,
    InStay,
    CheckingOutToday,
    Completed,
    Cancelled,
    NoShow
}