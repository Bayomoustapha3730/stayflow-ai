using System.Text;
using Microsoft.Extensions.Options;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services.Payments;

public interface IMpesaPaymentReconciliationService
{
    Task<int> ReconcileStalePaymentsAsync(CancellationToken cancellationToken);
}

public sealed class MpesaPaymentReconciliationService(
    IPaymentRepository paymentRepository,
    IMpesaApiClient mpesaApiClient,
    IMpesaCredentialResolver credentialResolver,
    IOptions<MpesaOptions> options,
    ILogger<MpesaPaymentReconciliationService> logger)
    : IMpesaPaymentReconciliationService
{
    public async Task<int> ReconcileStalePaymentsAsync(
        CancellationToken cancellationToken)
    {
        var mpesaOptions = options.Value;

        if (!mpesaOptions.Enabled || !mpesaOptions.ReconciliationEnabled)
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddSeconds(
            -mpesaOptions.ReconciliationPendingAgeSeconds);

        var payments = await paymentRepository.GetStaleMpesaPaymentsAsync(
            cutoff,
            mpesaOptions.ReconciliationBatchSize,
            cancellationToken);

        if (payments.Count == 0)
        {
            return 0;
        }

        var credentials =
            await credentialResolver.ResolveAsync(cancellationToken);

        if (!credentials.Success ||
            string.IsNullOrWhiteSpace(credentials.PassKey))
        {
            logger.LogWarning(
                "M-PESA reconciliation skipped because the passkey is unavailable.");
            return 0;
        }

        var reconciled = 0;

        foreach (var payment in payments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(payment.ProviderCheckoutRequestId))
            {
                continue;
            }

            // Re-check in case another callback/request updated the tracked entity
            // before this reconciliation cycle processes it.
            if (payment.Status.ToPaymentStatus().IsTerminal())
            {
                continue;
            }

            try
            {
                var timestamp = DateTime.UtcNow.ToString(
                    "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture);

                var password = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{mpesaOptions.ShortCode}{credentials.PassKey}{timestamp}"));

                var response = await mpesaApiClient.QueryStkPushAsync(
                    new MpesaStkQueryRequest(
                        mpesaOptions.ShortCode,
                        password,
                        timestamp,
                        payment.ProviderCheckoutRequestId),
                    cancellationToken);

                // ResponseCode indicates whether the QUERY itself was accepted.
                if (response.ResponseCode != 0)
                {
                    logger.LogWarning(
                        "Daraja STK query for payment {PaymentId} returned response code {ResponseCode}.",
                        payment.Id,
                        response.ResponseCode);

                    continue;
                }

                // No ResultCode yet means Daraja has not returned a terminal payment result.
                if (response.ResultCode is null)
                {
                    continue;
                }

                var changed = ApplyResult(
                    payment,
                    response.ResultCode.Value,
                    response.ResultDescription);

                if (!changed)
                {
                    logger.LogInformation(
                        "M-PESA payment {PaymentId} remains in progress. ResultCode={ResultCode}, ResultDesc={ResultDescription}.",
                        payment.Id,
                        response.ResultCode.Value,
                        response.ResultDescription);

                    continue;
                }

                await paymentRepository.SaveChangesAsync(cancellationToken);

                reconciled++;

                logger.LogInformation(
                    "Reconciled M-PESA payment {PaymentId}. ResultCode={ResultCode}, Status={Status}.",
                    payment.Id,
                    response.ResultCode.Value,
                    payment.Status);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is MpesaProviderException
                or HttpRequestException
                or TaskCanceledException
                or System.Text.Json.JsonException)
            {
                logger.LogWarning(
                    ex,
                    "M-PESA reconciliation failed for payment {PaymentId}; it will be retried.",
                    payment.Id);
            }
        }

        return reconciled;
    }

    private bool ApplyResult(
        Payment payment,
        int resultCode,
        string? resultDescription)
    {
        if (payment.Status.ToPaymentStatus().IsTerminal())
        {
            return false;
        }

        // Daraja 4999 means the transaction is still being processed.
        // This is explicitly non-terminal and must be retried later.
        if (resultCode == 4999)
        {
            var requestedAt =
                payment.RequestedAtUtc ?? payment.CreatedAt;

            var maxAge = TimeSpan.FromSeconds(
                options.Value.ReconciliationMaxAgeSeconds);

            if (DateTimeOffset.UtcNow - requestedAt >= maxAge)
            {
                var expiredAt = DateTimeOffset.UtcNow;

                payment.Status =
                    PaymentStatus.Expired.ToStorageValue();

                payment.FailureCode =
                    "STK_RECONCILIATION_TIMEOUT";

                payment.FailureMessage =
                    "M-PESA payment status could not be finalized within the reconciliation window.";

                payment.UpdatedAt = expiredAt;

                return true;
            }

            if (payment.Status == PaymentStatus.Pending.ToStorageValue())
            {
                payment.Status =
                    PaymentStatus.Processing.ToStorageValue();

                payment.UpdatedAt = DateTimeOffset.UtcNow;

                return true;
            }

            return false;
        }

        var now = DateTimeOffset.UtcNow;

        payment.FailureCode =
            resultCode == 0 ? null : resultCode.ToString();

        payment.FailureMessage =
            resultCode == 0 ? null : resultDescription;

        switch (resultCode)
        {
            case 0:
                payment.Status = PaymentStatus.Paid.ToStorageValue();
                payment.CompletedAtUtc = now;
                payment.FailedAtUtc = null;
                payment.CancelledAtUtc = null;
                break;

            case 1032:
                payment.Status = PaymentStatus.Cancelled.ToStorageValue();
                payment.CancelledAtUtc = now;
                break;

            case 1037:
                payment.Status = PaymentStatus.Failed.ToStorageValue();
                payment.FailedAtUtc = now;
                break;

            default:
                payment.Status = PaymentStatus.Failed.ToStorageValue();
                payment.FailedAtUtc = now;
                break;
        }

        payment.UpdatedAt = now;
        return true;
    }
}
