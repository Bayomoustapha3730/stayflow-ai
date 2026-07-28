using StayFlow.Api.DTOs.ConciergeActions;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ConciergeActionResultFormatter : IConciergeActionResultFormatter
{
    public string ToGuestMessage(ConciergeActionExecutionResult result)
    {
        return result.GuestSafeResultCode switch
        {
            ConciergeActionResponseCodes.EarlyCheckInRequestSubmitted => "I've submitted your early check-in request. The host still needs to approve it.",
            ConciergeActionResponseCodes.LateCheckoutRequestSubmitted => "I've submitted your late checkout request. The host still needs to approve it.",
            ConciergeActionResponseCodes.MaintenanceTicketCreated => "Your maintenance request has been submitted.",
            ConciergeActionResponseCodes.HousekeepingRequestSubmitted => "Your housekeeping request has been submitted.",
            ConciergeActionResponseCodes.ExtraItemRequestSubmitted => "Your extra-item request has been submitted.",
            ConciergeActionResponseCodes.ParkingRequestSubmitted => "I've submitted your parking request. The host will confirm availability.",
            ConciergeActionResponseCodes.HostNotified => "I've notified the host.",
            ConciergeActionResponseCodes.AlreadySubmitted => "That request was already submitted.",
            ConciergeActionResponseCodes.ActionNotAllowed => "I'm unable to submit that request for this reservation.",
            ConciergeActionResponseCodes.InvalidRequest => "I couldn't submit that request because some details are missing or invalid.",
            _ => "I couldn't submit that request right now. Nothing was changed. Please try again or contact the host."
        };
    }
}
