using Microsoft.Extensions.Options;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppOutboundSendGateTests
{
    public static TheoryData<WhatsAppSendOrigin> KnownOrigins => new()
    {
        WhatsAppSendOrigin.ManualHost,
        WhatsAppSendOrigin.AiConcierge,
        WhatsAppSendOrigin.GuestJourney,
        WhatsAppSendOrigin.ReservationLifecycle,
        WhatsAppSendOrigin.Retry,
        WhatsAppSendOrigin.TemplateManual,
        WhatsAppSendOrigin.SystemOther
    };

    public static TheoryData<WhatsAppSendOrigin> AutonomousOrigins => new()
    {
        WhatsAppSendOrigin.AiConcierge,
        WhatsAppSendOrigin.GuestJourney,
        WhatsAppSendOrigin.ReservationLifecycle,
        WhatsAppSendOrigin.Retry,
        WhatsAppSendOrigin.TemplateManual,
        WhatsAppSendOrigin.SystemOther
    };

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void EvaluateRealProviderSend_UnknownOrigin_DeniesWithSendOriginNotSpecified(int originValue)
    {
        var gate = CreateGate(productionSendingEnabled: true, manualHostProductionSendingEnabled: true);

        var result = gate.EvaluateRealProviderSend((WhatsAppSendOrigin)originValue, isIntegrationProductionEnabled: true);

        Assert.False(result.Success);
        Assert.Equal("SendOriginNotSpecified", result.FailureCode);
    }

    [Theory]
    [MemberData(nameof(KnownOrigins))]
    public void EvaluateRealProviderSend_IntegrationProductionDisabled_DeniesEveryOrigin(WhatsAppSendOrigin origin)
    {
        var gate = CreateGate(productionSendingEnabled: true, manualHostProductionSendingEnabled: true);

        var result = gate.EvaluateRealProviderSend(origin, isIntegrationProductionEnabled: false);

        Assert.False(result.Success);
        Assert.Equal("ProductionSendingDisabled", result.FailureCode);
    }

    [Theory]
    [MemberData(nameof(KnownOrigins))]
    public void EvaluateRealProviderSend_GlobalAndManualProductionDisabled_DeniesEveryOrigin(WhatsAppSendOrigin origin)
    {
        var gate = CreateGate(productionSendingEnabled: false, manualHostProductionSendingEnabled: false);

        var result = gate.EvaluateRealProviderSend(origin, isIntegrationProductionEnabled: true);

        Assert.False(result.Success);
    }

    [Fact]
    public void EvaluateRealProviderSend_ManualHostProductionEnabled_AllowsManualHostOnly()
    {
        var gate = CreateGate(productionSendingEnabled: false, manualHostProductionSendingEnabled: true);

        var result = gate.EvaluateRealProviderSend(WhatsAppSendOrigin.ManualHost, isIntegrationProductionEnabled: true);

        Assert.True(result.Success);
    }

    [Theory]
    [MemberData(nameof(AutonomousOrigins))]
    public void EvaluateRealProviderSend_ManualHostProductionEnabled_DeniesAutonomousOrigins(WhatsAppSendOrigin origin)
    {
        var gate = CreateGate(productionSendingEnabled: false, manualHostProductionSendingEnabled: true);

        var result = gate.EvaluateRealProviderSend(origin, isIntegrationProductionEnabled: true);

        Assert.False(result.Success);
        Assert.Equal("ProductionSendingDisabled", result.FailureCode);
    }

    [Theory]
    [MemberData(nameof(KnownOrigins))]
    public void EvaluateRealProviderSend_GlobalProductionEnabled_AllowsKnownOrigins(WhatsAppSendOrigin origin)
    {
        var gate = CreateGate(productionSendingEnabled: true, manualHostProductionSendingEnabled: false);

        var result = gate.EvaluateRealProviderSend(origin, isIntegrationProductionEnabled: true);

        Assert.True(result.Success);
    }

    private static WhatsAppOutboundSendGate CreateGate(bool productionSendingEnabled, bool manualHostProductionSendingEnabled)
    {
        return new WhatsAppOutboundSendGate(Options.Create(new WhatsAppCloudOptions
        {
            ProductionSendingEnabled = productionSendingEnabled,
            ManualHostProductionSendingEnabled = manualHostProductionSendingEnabled,
            DevelopmentMode = false
        }));
    }
}