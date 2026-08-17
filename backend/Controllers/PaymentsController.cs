using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Payments;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Tenant-scoped guest/reservation payment reads. Never accepts CompanyId from the caller;
/// the active tenant is derived exclusively from the authenticated context.
/// </summary>
[ApiController]
[Produces("application/json")]
[Authorize]
public sealed class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    [HttpPost("/api/payments/mpesa/stk")]
    [RequiresPermission("reservations.read")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> InitiateMpesaPayment(
        [FromBody] InitiateMpesaPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await paymentService.InitiateMpesaPaymentAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>Gets a single tenant-scoped payment by ID.</summary>
    [HttpGet("/api/payments/{id:guid}")]
    [RequiresPermission("reservations.read")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPayment(Guid id, CancellationToken cancellationToken)
    {
        var response = await paymentService.GetPaymentAsync(id, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    /// <summary>Gets tenant-scoped payments for a reservation belonging to the active tenant.</summary>
    [HttpGet("/api/reservations/{reservationId:guid}/payments")]
    [RequiresPermission("reservations.read")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<PaymentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<PaymentDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PaymentDto>>>> GetReservationPayments(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var response = await paymentService.GetReservationPaymentsAsync(reservationId, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }
}
