using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

/// <summary>
/// Synchronizes provider delivery receipts onto the durable GuestJourneyMessage after the existing
/// WhatsApp webhook has already resolved the ConversationMessage tenant-safely. Correlation is by
/// CompanyId + ConversationMessageId; no second webhook endpoint or provider lookup is introduced.
/// </summary>
public interface IGuestJourneyDeliveryReceiptSynchronizer
{
    Task<bool> SyncAsync(
        Guid companyId,
        Guid conversationMessageId,
        ConversationMessageDeliveryStatus deliveryStatus,
        DateTimeOffset occurredAt,
        string? failureCode,
        string? failureReason,
        CancellationToken cancellationToken);
}
