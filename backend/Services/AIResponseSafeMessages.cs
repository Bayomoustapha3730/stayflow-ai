namespace StayFlow.Api.Services;

public static class AIResponseSafeMessages
{
    public const string GeneralValidationFailure = "I need a team member to verify that before I reply.";
    public const string PropertyAccessVerificationRequired = "Access details require verification or host assistance. I can help contact the host.";
    public const string HostAssistanceRequired = "A host or support team member needs to help with this request.";
    public const string ResponseUnavailable = "I am sorry, I cannot provide a reliable answer right now. I can ask the host for help.";
    public const string OperationalApprovalCannotBeConfirmed = "I cannot confirm that approval from the host. I can help request host assistance.";
}
