using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services;

public sealed class WhatsAppOutboundSendGate(IOptions<WhatsAppCloudOptions> options) : IWhatsAppOutboundSendGate
{
    private const string ProductionDisabledCode = "ProductionSendingDisabled";
    private const string ProductionDisabledSummary = "WhatsApp production sending is disabled for this integration.";
    private const string ManualHostDisabledCode = "ManualHostProductionSendingDisabled";
    private const string ManualHostDisabledSummary = "Manual host WhatsApp production sending is disabled for this integration.";
    private const string UnknownOriginCode = "SendOriginNotSpecified";
    private const string UnknownOriginSummary = "WhatsApp sending requires an explicit send origin.";

    public WhatsAppOutboundSendGateResult EvaluateConfiguredSend(WhatsAppSendOrigin origin, bool isIntegrationProductionEnabled)
    {
        if (!IsKnownOrigin(origin))
        {
            return WhatsAppOutboundSendGateResult.Deny(UnknownOriginCode, UnknownOriginSummary);
        }

        // Development mode resolves the non-networked client, so no provider traffic is possible.
        return options.Value.DevelopmentMode
            ? WhatsAppOutboundSendGateResult.Allow()
            : EvaluateRealProviderSend(origin, isIntegrationProductionEnabled);
    }

    public WhatsAppOutboundSendGateResult EvaluateRealProviderSend(WhatsAppSendOrigin origin, bool isIntegrationProductionEnabled)
    {
        if (!IsKnownOrigin(origin))
        {
            return WhatsAppOutboundSendGateResult.Deny(UnknownOriginCode, UnknownOriginSummary);
        }

        // The per-integration production flag is mandatory for every origin, including ManualHost.
        if (!isIntegrationProductionEnabled)
        {
            return WhatsAppOutboundSendGateResult.Deny(ProductionDisabledCode, ProductionDisabledSummary);
        }

        if (origin == WhatsAppSendOrigin.ManualHost)
        {
            return options.Value.ProductionSendingEnabled || options.Value.ManualHostProductionSendingEnabled
                ? WhatsAppOutboundSendGateResult.Allow()
                : WhatsAppOutboundSendGateResult.Deny(ManualHostDisabledCode, ManualHostDisabledSummary);
        }

        // Every autonomous origin continues to require the global production flag.
        return options.Value.ProductionSendingEnabled
            ? WhatsAppOutboundSendGateResult.Allow()
            : WhatsAppOutboundSendGateResult.Deny(ProductionDisabledCode, ProductionDisabledSummary);
    }

    private static bool IsKnownOrigin(WhatsAppSendOrigin origin) => Enum.IsDefined(origin);
}