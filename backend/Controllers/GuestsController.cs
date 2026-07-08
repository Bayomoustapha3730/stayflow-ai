using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Guests;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Manages tenant-scoped guest profiles for StayFlow AI operators.
/// </summary>
[ApiController]
[Route("guests")]
[Produces("application/json")]
[Authorize]
public sealed class GuestsController(IGuestService guestService) : ControllerBase
{
    /// <summary>
    /// Gets tenant-scoped guests with pagination and optional search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<GuestSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<GuestSummaryDto>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<GuestSummaryDto>>>> GetGuests(
        [FromQuery] GuestQueryParameters query,
        CancellationToken cancellationToken)
    {
        var response = await guestService.GetAsync(query, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Gets one tenant-scoped guest by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GuestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GuestDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<GuestDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GuestDto>>> GetGuest(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await guestService.GetByIdAsync(id, cancellationToken);
        return response.Success ? Ok(response) : response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }

    /// <summary>
    /// Creates a tenant-scoped guest profile.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<GuestDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<GuestDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GuestDto>>> CreateGuest(
        [FromBody] CreateGuestRequest request,
        CancellationToken cancellationToken)
    {
        var response = await guestService.CreateAsync(request, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return BadRequest(response);
        }

        return CreatedAtAction(nameof(GetGuest), new { id = response.Data.Id }, response);
    }

    /// <summary>
    /// Updates a tenant-scoped guest profile.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GuestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GuestDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<GuestDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<GuestDto>>> UpdateGuest(
        Guid id,
        [FromBody] UpdateGuestRequest request,
        CancellationToken cancellationToken)
    {
        var response = await guestService.UpdateAsync(id, request, cancellationToken);
        if (response.Success)
        {
            return Ok(response);
        }

        return response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }

    /// <summary>
    /// Soft deletes a tenant-scoped guest profile.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteGuest(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await guestService.DeleteAsync(id, cancellationToken);
        return response.Success ? Ok(response) : response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }
}
