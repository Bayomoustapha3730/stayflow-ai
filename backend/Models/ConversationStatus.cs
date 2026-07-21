namespace StayFlow.Api.Models;

public enum ConversationStatus
{
    Open = 0,
    AwaitingHost = 1,
    Escalated = 2,
    HumanManaged = 3,
    Resolved = 4,
    Closed = 5,
    AwaitingGuest = 6
}
