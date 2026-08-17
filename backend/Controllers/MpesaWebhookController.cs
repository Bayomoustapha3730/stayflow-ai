using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Anonymous Safaricom Daraja STK Push callback endpoint. Safaricom does not provide a signature
/// scheme for this callback; hardening relies on strict payload validation, provider-identifier
/// correlation, and idempotent processing. CompanyId is never trusted from the payload.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("webhooks/mpesa")]
[EnableRateLimiting("mpesa-webhook")]
public sealed class MpesaWebhookController(
    IPaymentService paymentService,
    ILogger<MpesaWebhookController> logger) : ControllerBase
{
    private const int MaxBodyBytes = 64 * 1024;

    [HttpPost("stk")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> StkCallback(CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        if (System.Text.Encoding.UTF8.GetByteCount(rawBody) > MaxBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var result = await paymentService.HandleMpesaCallbackAsync(rawBody, cancellationToken);

        logger.LogInformation(
            "M-PESA STK callback processed with result {Result}. CorrelationId={CorrelationId}",
            result,
            HttpContext.TraceIdentifier);

        // Always acknowledge with 200 so Safaricom does not retry indefinitely; internal
        // outcome (duplicate/unknown/malformed) is tracked via logs and the webhook event table.
        return Ok(new { ResultCode = 0, ResultDesc = "Accepted" });
    }
}
