using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("webhooks/whatsapp")]
[EnableRateLimiting("whatsapp-webhook")]
public sealed class WhatsAppWebhookController(
    IOptions<WhatsAppCloudOptions> options,
    IWhatsAppWebhookSignatureVerifier signatureVerifier,
    IWhatsAppWebhookQueue webhookQueue,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    private const int MaxBodyBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<IActionResult> Verify([FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.verify_token")] string? verifyToken, [FromQuery(Name = "hub.challenge")] string? challenge, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        if (!string.Equals(mode, "subscribe", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(challenge))
        {
            return Unauthorized();
        }

        var valid = await signatureVerifier.IsWebhookVerificationTokenValidAsync(verifyToken, cancellationToken);
        return valid
            ? Content(challenge, "text/plain")
            : Unauthorized();
    }

    [HttpPost]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        Request.EnableBuffering();
        using var memoryStream = new MemoryStream();
        await Request.Body.CopyToAsync(memoryStream, cancellationToken);
        var rawBody = memoryStream.ToArray();
        Request.Body.Position = 0;

        if (rawBody.Length > MaxBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var signatureValidation = await signatureVerifier.ValidateSignatureAsync(rawBody, Request.Headers["X-Hub-Signature-256"], cancellationToken);
        if (!signatureValidation.IsValid)
        {
            logger.LogWarning("WhatsApp webhook signature validation failed. Reason={Reason} CorrelationId={CorrelationId}", signatureValidation.FailureReason, HttpContext.TraceIdentifier);
            return Unauthorized();
        }

        WhatsAppWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        if (payload is null)
        {
            return BadRequest();
        }

        await webhookQueue.EnqueueAsync(new QueuedWhatsAppWebhookEnvelope
        {
            CorrelationId = HttpContext.TraceIdentifier,
            Payload = payload
        }, cancellationToken);

        return Ok();
    }
}