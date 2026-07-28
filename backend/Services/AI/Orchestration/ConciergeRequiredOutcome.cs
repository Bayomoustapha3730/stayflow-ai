namespace StayFlow.Api.Services.AI.Orchestration;

public enum ConciergeRequiredOutcome
{
    GroundedAnswer = 0,
    MultiIntentGroundedAnswer = 1,
    MissingInformation = 2,
    Clarification = 3,
    Emergency = 4,
    HostVerificationRequired = 5
}