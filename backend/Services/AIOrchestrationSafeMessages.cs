namespace StayFlow.Api.Services;

public static class AIOrchestrationSafeMessages
{
    public const string ClarificationRequired = "I found more than one possible stay. Please confirm which stay you mean.";
    public const string HostAssistanceRequired = "I need a host or support team member to help with this request.";
    public const string NoEligibleReservation = "I could not verify an eligible stay for this request. I can help ask the host for assistance.";
    public const string ProviderUnavailable = "The AI assistant is temporarily unavailable. I can ask the host for help.";
    public const string GeneralResponseUnavailable = "I cannot provide a reliable answer right now. I can ask the host for help.";
}
