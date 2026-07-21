namespace StayFlow.Api.Models;

public enum ConversationStatus
{
    Open = 0,
    AwaitingGuest = 6,
    AwaitingHost = 1,
    Escalated = 2,
    HumanManaged = 3,
    Resolved = 4,
    Closed = 5
}
