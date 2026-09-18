using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppOutboundSendCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_AdmitsFeatureAndQuotaBeforeProviderDispatch()
    {
        var entitlement = new RecordingEntitlementService();
        var coordinator = new WhatsAppOutboundSendCoordinator(entitlement);
        var sends = 0;

        var result = await coordinator.ExecuteAsync(
            Guid.NewGuid(),
            "whatsapp:message:one",
            _ =>
            {
                sends++;
                return Task.FromResult("sent");
            },
            CancellationToken.None);

        Assert.Equal("sent", result);
        Assert.Equal(1, sends);
        Assert.Equal([FeatureKeys.WhatsApp], entitlement.FeatureKeys);
        Assert.Equal(["whatsapp:message:one"], entitlement.QuotaKeys);
    }

    [Fact]
    public async Task ExecuteAsync_QuotaExhaustionDoesNotDispatchProvider()
    {
        var entitlement = new RecordingEntitlementService
        {
            QuotaException = new QuotaExceededException("WhatsAppMessages", 1, 1, 1)
        };
        var coordinator = new WhatsAppOutboundSendCoordinator(entitlement);
        var sends = 0;

        await Assert.ThrowsAsync<QuotaExceededException>(() => coordinator.ExecuteAsync(
            Guid.NewGuid(),
            "whatsapp:message:exhausted",
            _ =>
            {
                sends++;
                return Task.FromResult(true);
            },
            CancellationToken.None));

        Assert.Equal(0, sends);
    }

    [Fact]
    public async Task ExecuteAsync_ReplayUsesSameLogicalQuotaIdentity()
    {
        var entitlement = new RecordingEntitlementService();
        var coordinator = new WhatsAppOutboundSendCoordinator(entitlement);
        var operationKey = "whatsapp:lifecycle:event-one";

        await coordinator.ExecuteAsync(GetGuid(operationKey), operationKey, _ => Task.FromResult(true), CancellationToken.None);
        await coordinator.ExecuteAsync(GetGuid(operationKey), operationKey, _ => Task.FromResult(true), CancellationToken.None);

        Assert.Equal([operationKey], entitlement.QuotaKeys);
    }

    [Fact]
    public async Task ExecuteAsync_FeatureDisabledDoesNotConsumeQuotaOrDispatchProvider()
    {
        var entitlement = new RecordingEntitlementService
        {
            FeatureException = new ForbiddenOperationException("WhatsApp is disabled.", "feature_not_enabled")
        };
        var coordinator = new WhatsAppOutboundSendCoordinator(entitlement);
        var sends = 0;

        await Assert.ThrowsAsync<ForbiddenOperationException>(() => coordinator.ExecuteAsync(
            Guid.NewGuid(),
            "whatsapp:message:disabled",
            _ =>
            {
                sends++;
                return Task.FromResult(true);
            },
            CancellationToken.None));

        Assert.Equal(0, sends);
        Assert.Empty(entitlement.QuotaKeys);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFailureDoesNotRefundOrConsumeReplay()
    {
        var entitlement = new RecordingEntitlementService();
        var coordinator = new WhatsAppOutboundSendCoordinator(entitlement);
        var sends = 0;

        async Task<bool> FailingProvider(CancellationToken cancellationToken)
        {
            sends++;
            await Task.CompletedTask;
            return false;
        }

        await coordinator.ExecuteAsync(Guid.NewGuid(), "whatsapp:message:failed", FailingProvider, CancellationToken.None);
        await coordinator.ExecuteAsync(Guid.NewGuid(), "whatsapp:message:failed", FailingProvider, CancellationToken.None);

        Assert.Equal(2, sends);
        Assert.Equal(["whatsapp:message:failed"], entitlement.QuotaKeys);
    }

    private sealed class RecordingEntitlementService : ISubscriptionEntitlementService
    {
        public List<string> FeatureKeys { get; } = [];
        public List<string> QuotaKeys { get; } = [];
        private HashSet<string> ConsumedKeys { get; } = new(StringComparer.Ordinal);
        public QuotaExceededException? QuotaException { get; init; }
        public ForbiddenOperationException? FeatureException { get; init; }

        public Task EnsureFeatureEnabledAsync(Guid companyId, string featureKey, CancellationToken cancellationToken)
        {
            FeatureKeys.Add(featureKey);
            if (FeatureException is not null)
            {
                throw FeatureException;
            }

            return Task.CompletedTask;
        }

        public Task<UsageConsumptionResult> ConsumeQuotaAsync(Guid companyId, UsageMetric metric, long quantity, string idempotencyKey, CancellationToken cancellationToken)
        {
            if (QuotaException is not null)
            {
                throw QuotaException;
            }

            if (!ConsumedKeys.Add(idempotencyKey))
            {
                return Task.FromResult(new UsageConsumptionResult(metric, 10, 0, 1, false, true));
            }

            QuotaKeys.Add(idempotencyKey);
            return Task.FromResult(new UsageConsumptionResult(metric, 10, 0, 1, false, false));
        }

        public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SubscriptionSnapshot> UpdatePlanAsync(Guid companyId, Guid? planId, string? planName, string? notes, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static Guid GetGuid(string value)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(value.GetHashCode()).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}
