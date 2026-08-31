using System.Globalization;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services.Payments;

/// <summary>
/// Posts a guest-facing payment confirmation into the reservation's conversation and relies on
/// the existing conversation message/realtime/outbound-channel pipeline for host realtime updates
/// and WhatsApp delivery. Grounded exclusively in ReservationPaymentGroundingService figures -
/// never infers or fabricates amounts. All failures are caught and logged; a notification failure
/// must never affect the already-persisted Paid payment.
/// </summary>
public sealed class PostPaymentNotificationService(
    IConversationRepository conversationRepository,
    IReservationPaymentGroundingService paymentGroundingService,
    IConversationService conversationService,
    ILogger<PostPaymentNotificationService> logger) : IPostPaymentNotificationService
{
    public async Task NotifyPaymentPaidAsync(Payment payment, CancellationToken cancellationToken)
    {
        try
        {
            if (payment.ReservationId is not { } reservationId)
            {
                return;
            }

            var grounding = await paymentGroundingService.GetReservationPaymentGroundingAsync(reservationId, payment.CompanyId, cancellationToken);
            if (grounding is null)
            {
                logger.LogWarning(
                    "Post-payment notification skipped: payment grounding unavailable for reservation {ReservationId}.",
                    reservationId);
                return;
            }

            var conversation = await conversationRepository.GetLatestConversationForReservationAsync(payment.CompanyId, reservationId, cancellationToken);
            if (conversation is null)
            {
                logger.LogInformation(
                    "Post-payment notification skipped: no conversation is bound to reservation {ReservationId}.",
                    reservationId);
                return;
            }

            var content = BuildConfirmationMessage(payment, grounding.Currency, grounding.RemainingBalance);
            var idempotencyKey = $"payment-confirmation:{payment.Id:D}";

            var result = await conversationService.AddPaymentConfirmationMessageAsync(
                payment.CompanyId,
                conversation.Id,
                content,
                idempotencyKey,
                cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Post-payment confirmation message was not stored for payment {PaymentId}: {Message}",
                    payment.Id,
                    result.Message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Post-payment notification failed for payment {PaymentId}.", payment.Id);
        }
    }

    private static string BuildConfirmationMessage(Payment payment, string currency, decimal? remainingBalance)
    {
        var amount = FormatAmount(payment.Amount);
        var receiptSuffix = string.IsNullOrWhiteSpace(payment.ProviderTransactionId)
            ? string.Empty
            : $" (Ref: {payment.ProviderTransactionId})";

        if (remainingBalance is not { } remaining || remaining <= 0m)
        {
            return $"Payment received. We have received your payment of {amount} {currency}{receiptSuffix}. Your reservation is now paid in full. Thank you.";
        }

        return $"Payment received. We have received your payment of {amount} {currency}{receiptSuffix}. Your remaining balance is {FormatAmount(remaining)} {currency}.";
    }

    private static string FormatAmount(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
}
