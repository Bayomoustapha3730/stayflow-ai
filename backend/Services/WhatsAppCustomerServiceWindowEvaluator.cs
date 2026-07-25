using Microsoft.Extensions.Options;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class WhatsAppCustomerServiceWindowEvaluator(
    IWhatsAppRepository whatsAppRepository,
    IOptions<WhatsAppCloudOptions> options) : IWhatsAppCustomerServiceWindowEvaluator
{
    public async Task<WhatsAppCustomerServiceWindowEvaluation> EvaluateAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
    {
        var inbound = await whatsAppRepository.GetLatestInboundGuestWhatsAppMessageAsync(companyId, conversationId, cancellationToken);
        if (inbound is null)
        {
            return new WhatsAppCustomerServiceWindowEvaluation
            {
                IsOpen = false,
                Reason = "No inbound WhatsApp guest message is available to open the customer-service window."
            };
        }

        var lastInboundAt = inbound.SentAt.ToUniversalTime();
        var expiresAt = lastInboundAt.AddHours(options.Value.CustomerServiceWindowHours);
        var now = DateTimeOffset.UtcNow;

        return new WhatsAppCustomerServiceWindowEvaluation
        {
            IsOpen = now <= expiresAt,
            LastInboundAt = lastInboundAt,
            ExpiresAt = expiresAt,
            Reason = now <= expiresAt
                ? "Customer-service window is open."
                : "Customer-service window has expired."
        };
    }
}
