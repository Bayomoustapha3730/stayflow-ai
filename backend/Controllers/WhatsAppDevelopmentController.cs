using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("development/whatsapp")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class WhatsAppDevelopmentController(
    IHostEnvironment environment,
    IWhatsAppWebhookQueue webhookQueue) : ControllerBase
{
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] WhatsAppWebhookPayload payload, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        await webhookQueue.EnqueueAsync(new QueuedWhatsAppWebhookEnvelope
        {
            CorrelationId = HttpContext.TraceIdentifier,
            Payload = payload
        }, cancellationToken);

        return Accepted();
    }
}