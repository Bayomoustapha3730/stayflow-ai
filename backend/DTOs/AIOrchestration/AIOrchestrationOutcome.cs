namespace StayFlow.Api.DTOs.AIOrchestration;

public enum AIOrchestrationOutcome
{
    Responded,
    ClarificationRequired,
    EscalationRequired,
    NoEligibleReservation,
    Blocked,
    ProviderUnavailable
}
