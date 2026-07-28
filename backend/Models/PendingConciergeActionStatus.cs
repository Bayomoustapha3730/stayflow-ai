namespace StayFlow.Api.Models;

public enum PendingConciergeActionStatus
{
    AwaitingGuestConfirmation = 0,
    AwaitingHostApproval = 1,
    ReadyToExecute = 2,
    Executing = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6,
    Expired = 7
}
