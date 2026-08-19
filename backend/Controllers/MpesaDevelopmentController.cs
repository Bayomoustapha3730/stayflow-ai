using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.DTOs.Payments;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Development-only M-PESA simulator. Safaricom Sandbox frequently returns 1032/1037 for STK Push,
/// which makes the successful payment path untestable end to end. This controller synthesizes the
/// exact Daraja success callback for an existing payment and pushes it through the production
/// callback processor, so no payment state transition logic is duplicated or bypassed.
/// Never available outside the Development environment.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/dev/mpesa")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class MpesaDevelopmentController(
    IHostEnvironment environment,
    IPaymentRepository paymentRepository,
    IPaymentService paymentService,
    ILogger<MpesaDevelopmentController> logger) : ControllerBase
{
    private const string SimulatedReceiptPrefix = "STAYFLOWDEV";
    private static readonly TimeSpan KenyaOffset = TimeSpan.FromHours(3);

    [HttpPost("payments/{paymentId:guid}/simulate-success")]
    public async Task<IActionResult> SimulateSuccess(Guid paymentId, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var payment = await paymentRepository.GetByIdWithoutTenantScopeAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            return NotFound(new { error = "payment_not_found", message = $"Payment {paymentId} was not found." });
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderRequestId) ||
            string.IsNullOrWhiteSpace(payment.ProviderCheckoutRequestId))
        {
            return BadRequest(new
            {
                error = "provider_identifiers_missing",
                message = "Payment has no ProviderRequestId/ProviderCheckoutRequestId; run an STK Push first."
            });
        }

        if (string.IsNullOrWhiteSpace(payment.CustomerPhoneNumber))
        {
            return BadRequest(new
            {
                error = "customer_phone_missing",
                message = "Payment has no CustomerPhoneNumber to include in the simulated callback."
            });
        }

        if (!IsSimulatable(payment.Status))
        {
            return Conflict(new
            {
                error = "payment_not_simulatable",
                message = $"Payment {paymentId} is in status '{payment.Status}'; only Pending or Processing payments can be simulated.",
                status = payment.Status
            });
        }

        var receipt = GenerateReceiptNumber();
        var rawBody = BuildSuccessCallbackJson(payment, receipt);

        var callbackResult = await paymentService.HandleMpesaCallbackAsync(rawBody, cancellationToken);

        logger.LogInformation(
            "Simulated M-PESA success callback for payment {PaymentId} produced result {CallbackResult}.",
            paymentId,
            callbackResult);

        if (callbackResult is not MpesaCallbackResult.Processed)
        {
            return Conflict(new
            {
                error = "callback_not_processed",
                message = $"Simulated callback was not processed ({callbackResult}).",
                callbackResult = callbackResult.ToString()
            });
        }

        var updated = await paymentRepository.GetByIdWithoutTenantScopeAsync(paymentId, cancellationToken)
                      ?? payment;

        return Ok(new MpesaSimulatedPaymentResultDto
        {
            PaymentId = updated.Id,
            Status = updated.Status,
            ProviderTransactionId = updated.ProviderTransactionId,
            CompletedAtUtc = updated.CompletedAtUtc,
            FailureCode = updated.FailureCode,
            FailureMessage = updated.FailureMessage
        });
    }

    private static bool IsSimulatable(string? status) =>
        string.Equals(status, PaymentStatus.Pending.ToStorageValue(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, PaymentStatus.Processing.ToStorageValue(), StringComparison.OrdinalIgnoreCase);

    private static string GenerateReceiptNumber() =>
        $"{SimulatedReceiptPrefix}{DateTime.UtcNow:yyyyMMddHHmmssfff}{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";

    private static string BuildSuccessCallbackJson(Payment payment, string receipt)
    {
        var transactionDate = long.Parse(
            DateTimeOffset.UtcNow.ToOffset(KenyaOffset).ToString("yyyyMMddHHmmss"),
            System.Globalization.CultureInfo.InvariantCulture);

        var callback = new
        {
            Body = new
            {
                stkCallback = new
                {
                    MerchantRequestID = payment.ProviderRequestId,
                    CheckoutRequestID = payment.ProviderCheckoutRequestId,
                    ResultCode = 0,
                    ResultDesc = "The service request is processed successfully.",
                    CallbackMetadata = new
                    {
                        Item = new object[]
                        {
                            new { Name = "Amount", Value = (object)payment.Amount },
                            new { Name = "MpesaReceiptNumber", Value = (object)receipt },
                            new { Name = "TransactionDate", Value = (object)transactionDate },
                            new { Name = "PhoneNumber", Value = (object)payment.CustomerPhoneNumber! }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(callback);
    }
}
