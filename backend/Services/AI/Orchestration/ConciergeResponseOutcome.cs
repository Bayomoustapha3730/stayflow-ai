namespace StayFlow.Api.Services.AI.Orchestration;

public enum ConciergeResponseOutcome
{
    Answered = 0,
    ClarificationRequired = 1,
    KnowledgeUnavailable = 2,
    EscalationRequired = 3
}
