namespace StayFlow.Api.DTOs.ReservationContext;

public enum ReservationContextResolutionOutcome
{
    Resolved,
    ClarificationRequired,
    EscalationRequired,
    NoEligibleReservation
}
