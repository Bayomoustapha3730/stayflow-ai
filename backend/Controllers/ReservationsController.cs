using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Reservations;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Manages tenant-scoped reservation records.
/// </summary>
[ApiController]
[Route("reservations")]
[Produces("application/json")]
[Authorize]
public sealed class ReservationsController(IReservationService reservationService) : ControllerBase
{
    /// <summary>
    /// Gets tenant-scoped reservations with pagination and optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReservationSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReservationSummaryDto>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<ReservationSummaryDto>>>> GetReservations(
        [FromQuery] ReservationQueryParameters query,
        CancellationToken cancellationToken)
    {
        var response = await reservationService.GetAsync(query, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Gets one tenant-scoped reservation by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReservationDto>>> GetReservation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await reservationService.GetByIdAsync(id, cancellationToken);
        return response.Success ? Ok(response) : response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }

    /// <summary>
    /// Creates a tenant-scoped reservation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ReservationDto>>> CreateReservation(
        [FromBody] CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await reservationService.CreateAsync(request, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return BadRequest(response);
        }

        return CreatedAtAction(nameof(GetReservation), new { id = response.Data.Id }, response);
    }

    /// <summary>
    /// Updates a tenant-scoped reservation.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReservationDto>>> UpdateReservation(
        Guid id,
        [FromBody] UpdateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await reservationService.UpdateAsync(id, request, cancellationToken);
        if (response.Success)
        {
            return Ok(response);
        }

        return response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }

    /// <summary>
    /// Transitions a tenant-scoped reservation through the approved lifecycle.
    /// </summary>
    [HttpPost("{id:guid}/status-transitions")]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ReservationDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReservationDto>>> TransitionReservationStatus(
        Guid id,
        [FromBody] TransitionReservationStatusRequest request,
        CancellationToken cancellationToken)
    {
        var response = await reservationService.TransitionStatusAsync(id, request, cancellationToken);
        if (response.Success)
        {
            return Ok(response);
        }

        return response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }

    /// <summary>
    /// Soft deletes a tenant-scoped reservation.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteReservation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await reservationService.DeleteAsync(id, cancellationToken);
        return response.Success ? Ok(response) : response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }
}
