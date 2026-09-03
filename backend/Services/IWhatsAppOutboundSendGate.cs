namespace StayFlow.Api.Services;

public interface IWhatsAppOutboundSendGate
{
    WhatsAppOutboundSendGateResult EvaluateConfiguredSend(bool isIntegrationProductionEnabled);
    WhatsAppOutboundSendGateResult EvaluateRealProviderSend(bool isIntegrationProductionEnabled);
}

public sealed record WhatsAppOutboundSendGateResult(bool Success, string? FailureCode = null, string? FailureSummary = null)
{
    public static WhatsAppOutboundSendGateResult Allow() => new(true);

    public static WhatsAppOutboundSendGateResult Deny(string failureCode, string failureSummary) => new(false, failureCode, failureSummary);
}