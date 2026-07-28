namespace StayFlow.Api.Models;

public enum ConciergeActionAuditEventType
{
    Detected = 0,
    ClarificationRequested = 1,
    ConfirmationRequested = 2,
    Confirmed = 3,
    Cancelled = 4,
    PolicyRejected = 5,
    ExecutionStarted = 6,
    ExecutionSucceeded = 7,
    ExecutionFailed = 8,
    IdempotentReplay = 9,
    HostApproved = 10,
    HostDeclined = 11
}
