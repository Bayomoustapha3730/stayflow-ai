using StayFlow.Api.Models;

namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ConciergeActionPolicy(ConciergeActionsOptions options) : IConciergeActionPolicy
{
    public (bool Allowed, string? FailureCode, string? ClarificationPrompt, ConciergeActionConfirmationRequirement ConfirmationRequirement, bool RequiresHostApproval) Validate(
        Conversation conversation,
        DTOs.ConciergeActions.ConciergeActionProposal proposal)
    {
        if (proposal.ActionType == ConciergeActionType.None)
        {
            return (false, "NoAction", null, ConciergeActionConfirmationRequirement.None, false);
        }

        if (conversation.Status == ConversationStatus.Closed || conversation.HumanTakeoverEnabled)
        {
            return (false, "ConversationNotActionable", null, ConciergeActionConfirmationRequirement.None, false);
        }

        if (!conversation.PropertyId.HasValue)
        {
            return (false, "MissingProperty", "I need to verify your reservation context before I can submit that request.", ConciergeActionConfirmationRequirement.None, false);
        }

        if (proposal.RequiresClarification)
        {
            return (false, "ClarificationRequired", proposal.ClarificationPrompt, ConciergeActionConfirmationRequirement.None, false);
        }

        return proposal.ActionType switch
        {
            ConciergeActionType.RequestEarlyCheckIn =>
                conversation.ReservationId.HasValue && options.EnableEarlyCheckIn
                    ? (true, null, null, ConciergeActionConfirmationRequirement.Both, true)
                    : (false, "ReservationRequired", "I can submit that after I verify your reservation.", ConciergeActionConfirmationRequirement.None, false),

            ConciergeActionType.RequestLateCheckout =>
                conversation.ReservationId.HasValue && options.EnableLateCheckout
                    ? (true, null, null, ConciergeActionConfirmationRequirement.Both, true)
                    : (false, "ReservationRequired", "I can submit that after I verify your reservation.", ConciergeActionConfirmationRequirement.None, false),

            ConciergeActionType.CreateMaintenanceTicket =>
                options.EnableMaintenance
                    ? (true, null, null, ConciergeActionConfirmationRequirement.None, false)
                    : (false, "Disabled", null, ConciergeActionConfirmationRequirement.None, false),

            ConciergeActionType.RequestHousekeeping =>
                conversation.ReservationId.HasValue && options.EnableHousekeeping
                    ? (true, null, null, ConciergeActionConfirmationRequirement.None, false)
                    : (false, "ReservationRequired", "I can submit housekeeping requests after I verify your reservation.", ConciergeActionConfirmationRequirement.None, false),

            ConciergeActionType.RequestExtraItem =>
                conversation.ReservationId.HasValue
                    ? (true, null, null, ConciergeActionConfirmationRequirement.None, false)
                    : (false, "ReservationRequired", "I can submit item requests after I verify your reservation.", ConciergeActionConfirmationRequirement.None, false),

            ConciergeActionType.RequestParking =>
                conversation.ReservationId.HasValue && options.EnableParking
                    ? (true, null, null, ConciergeActionConfirmationRequirement.Both, true)
                    : (false, "ReservationRequired", "I can submit parking requests after I verify your reservation.", ConciergeActionConfirmationRequirement.None, false),

            ConciergeActionType.RequestPayment =>
                conversation.ReservationId.HasValue && conversation.PropertyId.HasValue
                    ? (true, null, null, ConciergeActionConfirmationRequirement.ExplicitGuestConfirmation, false)
                    : (false, "ReservationRequired", "I can send the payment request after I verify your reservation context.", ConciergeActionConfirmationRequirement.None, false),

            ConciergeActionType.NotifyHost =>
                options.EnableHostNotification
                    ? (true, null, null, ConciergeActionConfirmationRequirement.ExplicitGuestConfirmation, false)
                    : (false, "Disabled", null, ConciergeActionConfirmationRequirement.None, false),

            _ => (false, "UnsupportedAction", null, ConciergeActionConfirmationRequirement.None, false)
        };
    }
}
