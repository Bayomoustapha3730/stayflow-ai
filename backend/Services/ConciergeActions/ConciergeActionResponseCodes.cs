namespace StayFlow.Api.Services.ConciergeActions;

public static class ConciergeActionResponseCodes
{
    public const string EarlyCheckInRequestSubmitted = "EarlyCheckInRequestSubmitted";
    public const string LateCheckoutRequestSubmitted = "LateCheckoutRequestSubmitted";
    public const string MaintenanceTicketCreated = "MaintenanceTicketCreated";
    public const string HousekeepingRequestSubmitted = "HousekeepingRequestSubmitted";
    public const string ExtraItemRequestSubmitted = "ExtraItemRequestSubmitted";
    public const string ParkingRequestSubmitted = "ParkingRequestSubmitted";
    public const string PaymentRequestSubmitted = "PaymentRequestSubmitted";
    public const string HostNotified = "HostNotified";
    public const string AlreadySubmitted = "AlreadySubmitted";
    public const string AwaitingHostApproval = "AwaitingHostApproval";
    public const string ActionNotAllowed = "ActionNotAllowed";
    public const string InvalidRequest = "InvalidRequest";
    public const string TemporaryFailure = "TemporaryFailure";
}
