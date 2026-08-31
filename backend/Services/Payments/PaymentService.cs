using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Payments;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services.Payments;

public sealed class PaymentService(
    IPaymentRepository paymentRepository,
    ICurrentTenantContext currentTenantContext,
    IKenyanPhoneNumberNormalizer phoneNumberNormalizer,
    IMpesaApiClient mpesaApiClient,
    IMpesaCredentialResolver credentialResolver,
    IOptions<MpesaOptions> mpesaOptions,
    ILogger<PaymentService> logger) : IPaymentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApiResponse<PaymentDto>> InitiateMpesaPaymentAsync(
        InitiateMpesaPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out _, out var tenantError))
        {
            return ApiResponse<PaymentDto>.Fail(tenantError, [tenantError]);
        }

        if (!mpesaOptions.Value.Enabled)
        {
            return ApiResponse<PaymentDto>.Fail("M-PESA payments are not enabled.");
        }

        if (request.ReservationId == Guid.Empty)
        {
            return ApiResponse<PaymentDto>.Fail("A reservation is required to initiate M-PESA payment.");
        }

        if (!phoneNumberNormalizer.TryNormalize(request.CustomerPhoneNumber, out var phoneNumber))
        {
            return ApiResponse<PaymentDto>.Fail("Enter a valid Kenyan mobile number.");
        }

        var reservation = await paymentRepository.GetReservationForPaymentAsync(request.ReservationId, companyId, cancellationToken);
        if (reservation is null || reservation.Property.CompanyId != companyId || reservation.PrimaryGuest.CompanyId != companyId)
        {
            return ApiResponse<PaymentDto>.Fail("Reservation was not found.");
        }

        if (reservation.BookingAmount is not { } bookingAmount || bookingAmount <= 0)
        {
            return ApiResponse<PaymentDto>.Fail("Reservation does not have a valid booking amount.");
        }

        var amount = request.AmountOverride ?? bookingAmount;
        if (amount <= 0 || amount > bookingAmount)
        {
            return ApiResponse<PaymentDto>.Fail("Payment amount is outside the reservation amount.");
        }

        if (!string.Equals(reservation.Currency, "KES", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<PaymentDto>.Fail("Only reservations priced in KES can be paid through M-PESA.");
        }

        var externalReference = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"stayflow-{Guid.NewGuid():N}"
            : request.IdempotencyKey.Trim();
        var existing = await paymentRepository.GetByExternalReferenceAsync(externalReference, companyId, cancellationToken);
        if (existing is not null)
        {
            return ApiResponse<PaymentDto>.Ok(MapToDto(existing), "Payment request already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ReservationId = reservation.Id,
            PropertyId = reservation.PropertyId,
            GuestId = reservation.PrimaryGuestId,
            Amount = amount,
            Currency = "KES",
            Provider = "M-PESA",
            ProviderEnvironment = mpesaOptions.Value.Environment,
            PaymentMethod = "STKPush",
            CustomerPhoneNumber = phoneNumber,
            ExternalReference = externalReference,
            InternalReference = reservation.ConfirmationNumber ?? $"reservation-{reservation.Id:N}",
            Status = PaymentStatus.Processing.ToStorageValue(),
            RequestedAtUtc = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        await paymentRepository.AddAsync(payment, cancellationToken);
        await paymentRepository.SaveChangesAsync(cancellationToken);

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var credentials = await credentialResolver.ResolveAsync(cancellationToken);
            if (!credentials.Success || string.IsNullOrWhiteSpace(credentials.PassKey))
            {
                throw new MpesaProviderException(credentials.FailureSummary ?? "M-PESA credentials are not configured.");
            }

            var password = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{mpesaOptions.Value.ShortCode}{credentials.PassKey}{timestamp}"));
            var response = await mpesaApiClient.InitiateStkPushAsync(new MpesaStkPushRequest(
                mpesaOptions.Value.ShortCode,
                password,
                timestamp,
                mpesaOptions.Value.TransactionType,
                amount,
                phoneNumber,
                mpesaOptions.Value.ShortCode,
                phoneNumber,
                $"{mpesaOptions.Value.CallbackBaseUrl.TrimEnd('/')}/webhooks/mpesa/stk",
                payment.InternalReference,
                request.Description ?? "StayFlow reservation payment"), cancellationToken);

            if (response.ResponseCode == 0 &&
                (string.IsNullOrWhiteSpace(response.MerchantRequestId) || string.IsNullOrWhiteSpace(response.CheckoutRequestId)))
            {
                throw new MpesaProviderException("Safaricom returned an incomplete payment response.");
            }

            payment.ProviderRequestId = response.MerchantRequestId;
            payment.ProviderCheckoutRequestId = response.CheckoutRequestId;
            payment.Status = response.ResponseCode == 0
                ? PaymentStatus.Pending.ToStorageValue()
                : PaymentStatus.Failed.ToStorageValue();
            payment.FailureCode = response.ResponseCode == 0 ? null : response.ResponseCode.ToString();
            payment.FailureMessage = response.ResponseCode == 0 ? null : "Safaricom did not accept the payment request.";
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            await paymentRepository.SaveChangesAsync(cancellationToken);
            return ApiResponse<PaymentDto>.Ok(MapToDto(payment), "Payment request sent to the guest's phone.");
        }
        catch (MpesaProviderException ex)
        {
            payment.Status = PaymentStatus.Failed.ToStorageValue();
            payment.FailureMessage = ex.Message;
            payment.FailedAtUtc = DateTimeOffset.UtcNow;
            await paymentRepository.SaveChangesAsync(cancellationToken);
            return ApiResponse<PaymentDto>.Fail(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            payment.Status = PaymentStatus.Failed.ToStorageValue();
            payment.FailureMessage = "M-PESA could not be reached. Please try again.";
            payment.FailedAtUtc = DateTimeOffset.UtcNow;
            await paymentRepository.SaveChangesAsync(cancellationToken);
            return ApiResponse<PaymentDto>.Fail(payment.FailureMessage);
        }
    }

    public async Task<ApiResponse<PaymentDto>> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out _, out var tenantError))
        {
            return ApiResponse<PaymentDto>.Fail(tenantError, [tenantError]);
        }

        var payment = await paymentRepository.GetByIdAsync(id, companyId, cancellationToken);
        return payment is null
            ? ApiResponse<PaymentDto>.Fail("Payment was not found.")
            : ApiResponse<PaymentDto>.Ok(MapToDto(payment));
    }

    public async Task<ApiResponse<IReadOnlyCollection<PaymentDto>>> GetReservationPaymentsAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out _, out var tenantError))
        {
            return ApiResponse<IReadOnlyCollection<PaymentDto>>.Fail(tenantError, [tenantError]);
        }

        if (!await paymentRepository.ReservationBelongsToCompanyAsync(reservationId, companyId, cancellationToken))
        {
            return ApiResponse<IReadOnlyCollection<PaymentDto>>.Fail("Reservation was not found.");
        }

        var payments = await paymentRepository.GetByReservationIdAsync(reservationId, companyId, cancellationToken);
        return ApiResponse<IReadOnlyCollection<PaymentDto>>.Ok(payments.Select(MapToDto).ToList());
    }

    public async Task<MpesaCallbackResult> HandleMpesaCallbackAsync(string rawBody, CancellationToken cancellationToken)
    {
        var stkCallback = TryParseCallback(rawBody);
        if (stkCallback is null || string.IsNullOrWhiteSpace(stkCallback.CheckoutRequestId))
        {
            logger.LogWarning("M-PESA callback rejected: malformed payload or missing CheckoutRequestID.");
            return MpesaCallbackResult.MalformedIgnored;
        }

        if (stkCallback.ResultCode == 0 && string.IsNullOrWhiteSpace(ExtractReceiptNumber(stkCallback)))
        {
            logger.LogWarning(
                "M-PESA success callback rejected because receipt metadata is missing for CheckoutRequestID {CheckoutRequestId}.",
                stkCallback.CheckoutRequestId);
            return MpesaCallbackResult.MalformedIgnored;
        }

        // Never trust a CompanyId from the callback payload; correlate strictly by provider-issued CheckoutRequestID.
        var payment = await paymentRepository.GetByCheckoutRequestIdAsync(stkCallback.CheckoutRequestId, cancellationToken);
        if (payment is null || string.IsNullOrWhiteSpace(stkCallback.MerchantRequestId) ||
            !string.Equals(payment.ProviderRequestId, stkCallback.MerchantRequestId, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "M-PESA callback ignored: provider identifiers did not match a payment for CheckoutRequestID {CheckoutRequestId}.",
                stkCallback.CheckoutRequestId);
            return MpesaCallbackResult.UnknownCheckoutRequestIgnored;
        }

        if (IsTerminalStatus(payment.Status))
        {
            logger.LogInformation(
                "M-PESA callback ignored for terminal payment state {PaymentStatus} on CheckoutRequestID {CheckoutRequestId}.",
                payment.Status,
                stkCallback.CheckoutRequestId);
            return MpesaCallbackResult.DuplicateIgnored;
        }

        var eventId = !string.IsNullOrWhiteSpace(stkCallback.MerchantRequestId)
            ? $"{stkCallback.MerchantRequestId}:{stkCallback.CheckoutRequestId}"
            : stkCallback.CheckoutRequestId;

        var webhookEvent = new PaymentWebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = "M-PESA",
            EventId = eventId,
            EventType = "STKPushCallback",
            CheckoutRequestId = stkCallback.CheckoutRequestId,
            TransactionId = ExtractReceiptNumber(stkCallback),
            EventCreatedAtUtc = DateTimeOffset.UtcNow,
            PayloadHash = ComputePayloadHash(rawBody),
            ProcessedAtUtc = DateTimeOffset.UtcNow
        };

        var isNewEvent = await paymentRepository.TryRecordWebhookEventAsync(webhookEvent, cancellationToken);
        if (!isNewEvent)
        {
            logger.LogInformation(
                "M-PESA callback duplicate ignored for CheckoutRequestID {CheckoutRequestId}.",
                stkCallback.CheckoutRequestId);
            return MpesaCallbackResult.DuplicateIgnored;
        }

        ApplyCallbackToPayment(payment, stkCallback);

        await AddAuditLogAsync(payment, cancellationToken);

        try
        {
            await paymentRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent retry of the same callback raced past the AnyAsync check; the unique
            // (Provider, EventId) index rejected the duplicate. Treat as already-processed.
            logger.LogInformation(
                "M-PESA callback duplicate detected at persistence for CheckoutRequestID {CheckoutRequestId}.",
                stkCallback.CheckoutRequestId);
            return MpesaCallbackResult.DuplicateIgnored;
        }

        return MpesaCallbackResult.Processed;
    }

    private static void ApplyCallbackToPayment(Payment payment, MpesaStkCallback stkCallback)
    {
        if (IsTerminalStatus(payment.Status))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        payment.ProviderRequestId ??= stkCallback.MerchantRequestId;

        if (stkCallback.ResultCode == 0)
        {
            payment.Status = PaymentStatus.Paid.ToStorageValue();
            payment.ProviderTransactionId = ExtractReceiptNumber(stkCallback);
            payment.CompletedAtUtc = now;
            payment.FailureCode = null;
            payment.FailureMessage = null;

            if (ExtractPhoneNumber(stkCallback) is { } phoneNumber)
            {
                payment.CustomerPhoneNumber = phoneNumber;
            }

            return;
        }

        // Non-zero ResultCode: map to Failed/Cancelled. Customer-initiated cancellation is 1032 on Daraja.
        payment.Status = stkCallback.ResultCode == 1032
            ? PaymentStatus.Cancelled.ToStorageValue()
            : PaymentStatus.Failed.ToStorageValue();

        payment.FailureCode = stkCallback.ResultCode.ToString();
        payment.FailureMessage = SanitizeFailureMessage(stkCallback.ResultDesc);

        if (payment.Status == PaymentStatus.Cancelled.ToStorageValue())
        {
            payment.CancelledAtUtc = now;
        }
        else
        {
            payment.FailedAtUtc = now;
        }
    }

    private static bool IsTerminalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return string.Equals(status, PaymentStatus.Paid.ToStorageValue(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, PaymentStatus.Failed.ToStorageValue(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, PaymentStatus.Cancelled.ToStorageValue(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task AddAuditLogAsync(Payment payment, CancellationToken cancellationToken)
    {
        await paymentRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Payment),
            EntityId = payment.Id,
            Action = "MpesaCallbackProcessed",
            Details = JsonSerializer.Serialize(new
            {
                payment.CompanyId,
                payment.ReservationId,
                Status = payment.Status,
                payment.ProviderTransactionId
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static string? ExtractReceiptNumber(MpesaStkCallback stkCallback) =>
        ExtractMetadataValue(stkCallback, "MpesaReceiptNumber")?.ToString();

    private static string? ExtractPhoneNumber(MpesaStkCallback stkCallback) =>
        ExtractMetadataValue(stkCallback, "PhoneNumber")?.ToString();

    private static object? ExtractMetadataValue(MpesaStkCallback stkCallback, string name)
    {
        var item = stkCallback.CallbackMetadata?.Item.FirstOrDefault(
            entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

        return item?.Value is JsonElement element
            ? element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                _ => null
            }
            : item?.Value;
    }

    private static string SanitizeFailureMessage(string? resultDesc)
    {
        if (string.IsNullOrWhiteSpace(resultDesc))
        {
            return "Payment was not completed.";
        }

        // Provider descriptions are safe to surface (no secrets); still cap length defensively.
        return resultDesc.Length > 500 ? resultDesc[..500] : resultDesc;
    }

    private MpesaStkCallback? TryParseCallback(string rawBody)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MpesaStkCallbackEnvelope>(rawBody, JsonOptions);
            return envelope?.Body?.StkCallback;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ComputePayloadHash(string rawBody)
    {
        var bytes = Encoding.UTF8.GetBytes(rawBody);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static bool IsUniqueConstraintViolation(Exception ex) =>
        ex.GetType().Name.Contains("DbUpdateException", StringComparison.Ordinal);

    private static PaymentDto MapToDto(Payment payment) => new()
    {
        Id = payment.Id,
        ReservationId = payment.ReservationId,
        PropertyId = payment.PropertyId,
        GuestId = payment.GuestId,
        Amount = payment.Amount,
        Currency = payment.Currency,
        Provider = payment.Provider,
        PaymentMethod = payment.PaymentMethod,
        Status = payment.Status,
        ProviderTransactionId = payment.ProviderTransactionId,
        CustomerPhoneNumber = payment.CustomerPhoneNumber,
        InternalReference = payment.InternalReference,
        FailureMessage = payment.FailureMessage,
        RequestedAtUtc = payment.RequestedAtUtc,
        CompletedAtUtc = payment.CompletedAtUtc,
        FailedAtUtc = payment.FailedAtUtc,
        CancelledAtUtc = payment.CancelledAtUtc,
        CreatedAt = payment.CreatedAt
    };

    private bool TryGetCompanyId(out Guid companyId, out Guid userId, out string error)
    {
        if (!currentTenantContext.IsAuthenticated)
        {
            companyId = Guid.Empty;
            userId = Guid.Empty;
            error = "Authenticated tenant context is required.";
            return false;
        }

        if (currentTenantContext.CompanyId is not { } tenantCompanyId || tenantCompanyId == Guid.Empty)
        {
            companyId = Guid.Empty;
            userId = Guid.Empty;
            error = "Authenticated tenant context is missing or invalid.";
            return false;
        }

        if (currentTenantContext.UserId is not { } tenantUserId || tenantUserId == Guid.Empty)
        {
            companyId = Guid.Empty;
            userId = Guid.Empty;
            error = "Authenticated user context is required for host payment actions.";
            return false;
        }

        companyId = tenantCompanyId;
        userId = tenantUserId;
        error = string.Empty;
        return true;
    }
}
