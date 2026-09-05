namespace StayFlow.Api.Services;

public interface IWhatsAppOutboundSendGate
{
    WhatsAppOutboundSendGateResult EvaluateConfiguredSend(WhatsAppSendOrigin origin, bool isIntegrationProductionEnabled);
    WhatsAppOutboundSendGateResult EvaluateRealProviderSend(WhatsAppSendOrigin origin, bool isIntegrationProductionEnabled);
}

public sealed record WhatsAppOutboundSendGateResult(bool Success, string? FailureCode = null, string? FailureSummary = null)
{
    public static WhatsAppOutboundSendGateResult Allow() => new(true);

    public static WhatsAppOutboundSendGateResult Deny(string failureCode, string failureSummary) => new(false, failureCode, failureSummary);
}