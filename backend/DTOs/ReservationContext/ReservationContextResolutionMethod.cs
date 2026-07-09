namespace StayFlow.Api.DTOs.ReservationContext;

public enum ReservationContextResolutionMethod
{
    VerifiedConversationBinding,
    ExplicitReservationReference,
    ExplicitPropertyName,
    SingleActiveReservation,
    SingleUpcomingReservation
}
