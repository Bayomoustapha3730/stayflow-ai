namespace StayFlow.Api.Models;

// Represents an automation opportunity tied to a property-local calendar date, not a message.
public enum ReservationLifecycleEventType
{
    PreArrival,
    ArrivalDay,
    InStay,
    CheckoutDay,
    PostStay
}
