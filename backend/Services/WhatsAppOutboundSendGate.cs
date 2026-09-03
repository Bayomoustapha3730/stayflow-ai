using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services;

public sealed class WhatsAppOutboundSendGate(IOptions<WhatsAppCloudOptions> options) : IWhatsAppOutboundSendGate
{
    private const string FailureCode = "ProductionSendingDisabled";
    private const string FailureSummary = "WhatsApp production sending is disabled for this integration.";

    public WhatsAppOutboundSendGateResult EvaluateConfiguredSend(bool isIntegrationProductionEnabled)
    {
        return options.Value.DevelopmentMode
            ? WhatsAppOutboundSendGateResult.Allow()
            : EvaluateRealProviderSend(isIntegrationProductionEnabled);
    }

    public WhatsAppOutboundSendGateResult EvaluateRealProviderSend(bool isIntegrationProductionEnabled)
    {
        if (!options.Value.ProductionSendingEnabled || !isIntegrationProductionEnabled)
        {
            return WhatsAppOutboundSendGateResult.Deny(FailureCode, FailureSummary);
        }

        return WhatsAppOutboundSendGateResult.Allow();
    }
}