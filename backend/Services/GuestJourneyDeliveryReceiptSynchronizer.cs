using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class GuestJourneyDeliveryReceiptSynchronizer(
    IGuestJourneyMessageRepository repository,
    ILogger<GuestJourneyDeliveryReceiptSynchronizer> logger) : IGuestJourneyDeliveryReceiptSynchronizer
{
    public async Task<bool> SyncAsync(
        Guid companyId,
        Guid conversationMessageId,
        ConversationMessageDeliveryStatus deliveryStatus,
        DateTimeOffset occurredAt,
        string? failureCode,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var message = await repository.FindByConversationMessageAsync(companyId, conversationMessageId, cancellationToken);
        if (message is null)
        {
            return false;
        }

        switch (deliveryStatus)
        {
            case ConversationMessageDeliveryStatus.Sent:
                // Provider acceptance only. Accepted was already recorded at send time and must not
                // be upgraded to Delivered without a real delivery receipt.
                return false;

            case ConversationMessageDeliveryStatus.Delivered:
            case ConversationMessageDeliveryStatus.Read:
                if (message.Status is GuestJourneyMessageStatus.Suppressed or GuestJourneyMessageStatus.Blocked)
                {
                    return false;
                }

                // "read" implies delivery even if the delivered receipt was missed.
                message.DeliveredAtUtc ??= occurredAt;
                message.Status = GuestJourneyMessageStatus.Delivered;
                break;

            case ConversationMessageDeliveryStatus.Failed:
                if (message.Status is GuestJourneyMessageStatus.Suppressed or GuestJourneyMessageStatus.Blocked)
                {
                    return false;
                }

                // Terminal for this intent rather than Failed: the provider already accepted the
                // message, and Failed with no NextAttemptAtUtc re-enters RecoverRetryableFailedAsync,
                // which would re-send something Meta may already have delivered.
                message.Status = GuestJourneyMessageStatus.Blocked;
                message.FailedAtUtc = occurredAt;
                message.LastError = Truncate(BuildFailureSummary(failureCode, failureReason));
                break;

            default:
                return false;
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Guest journey delivery receipt synchronized. GuestJourneyMessageId={GuestJourneyMessageId} CompanyId={CompanyId} ConversationMessageId={ConversationMessageId} DeliveryStatus={DeliveryStatus} Status={Status}.",
            message.Id,
            companyId,
            conversationMessageId,
            deliveryStatus,
            message.Status);

        return true;
    }

    private static string BuildFailureSummary(string? failureCode, string? failureReason)
    {
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return string.IsNullOrWhiteSpace(failureCode) ? failureReason : $"{failureCode}: {failureReason}";
        }

        return string.IsNullOrWhiteSpace(failureCode)
            ? "WhatsApp reported a delivery failure for this lifecycle message."
            : failureCode;
    }

    private static string Truncate(string value) => value.Length > 500 ? value[..500] : value;
}
