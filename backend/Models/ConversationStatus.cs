namespace StayFlow.Api.Models;

public enum ConversationStatus
{
    Open,
    AwaitingGuest,
    AwaitingHost,
    Escalated,
    HumanManaged,
    Resolved,
    Closed
}
