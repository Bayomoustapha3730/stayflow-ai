using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Billing;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/billing")]
[Produces("application/json")]
public sealed class BillingController(
    IBillingService billingService,
    IOptions<BillingOptions> billingOptions) : ControllerBase
{
    [HttpPost("checkout")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<CreateCheckoutSessionResponse>>> CreateCheckout(
        [FromBody] CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await billingService.CreateCheckoutSessionAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("portal")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<CreateBillingPortalSessionResponse>>> CreatePortal(CancellationToken cancellationToken)
    {
        var response = await billingService.CreateBillingPortalSessionAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("subscription")]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    public async Task<ActionResult<ApiResponse<BillingSubscriptionResponse>>> GetSubscription(CancellationToken cancellationToken)
    {
        var response = await billingService.GetSubscriptionAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("invoices")]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TenantInvoiceDto>>>> GetInvoices(CancellationToken cancellationToken)
    {
        var response = await billingService.GetInvoicesAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [AllowAnonymous]
    [HttpPost("webhook/stripe")]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("Stripe-Signature", out var signatures))
        {
            return Unauthorized();
        }

        if (Request.ContentLength is > 0 && Request.ContentLength > billingOptions.Value.WebhookMaxBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        if (Encoding.UTF8.GetByteCount(payload) > billingOptions.Value.WebhookMaxBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var result = await billingService.ProcessStripeWebhookAsync(payload, signatures.ToString(), cancellationToken);
        return Ok(new
        {
            result.EventId,
            result.EventType,
            result.WasDuplicate
        });
    }
}