namespace StayFlow.Api.Models;

/// <summary>
/// Authoritative values a lifecycle template parameter may be bound to. StayFlow only knows a
/// template's positional VariableCount (ComponentsJson is stored raw and never parsed for
/// semantics), so binding order must be configured explicitly rather than guessed.
/// </summary>
public enum ReservationLifecycleTemplateParameter
{
    GuestFirstName,
    PropertyName,
    CheckInDate,
    CheckOutDate
}
