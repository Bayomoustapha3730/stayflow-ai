using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class WhatsAppOutboundSendCoordinator(
    ISubscriptionEntitlementService subscriptionEntitlementService) : IWhatsAppOutboundSendCoordinator
{
    public async Task<T> ExecuteAsync<T>(
        Guid companyId,
        string operationKey,
        Func<CancellationToken, Task<T>> send,
        CancellationToken cancellationToken)
    {
        await subscriptionEntitlementService.EnsureFeatureEnabledAsync(
            companyId,
            FeatureKeys.WhatsApp,
            cancellationToken);

        await subscriptionEntitlementService.ConsumeQuotaAsync(
            companyId,
            UsageMetric.WhatsAppMessages,
            1,
            operationKey,
            cancellationToken);

        return await send(cancellationToken);
    }
}
