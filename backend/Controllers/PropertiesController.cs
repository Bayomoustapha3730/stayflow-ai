using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Properties;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Manages StayFlow AI rental properties and their guest-facing knowledge.
/// </summary>
[ApiController]
[Route("properties")]
[Produces("application/json")]
[Authorize]
public sealed class PropertiesController(IPropertyService propertyService) : ControllerBase
{
    /// <summary>
    /// Gets tenant-scoped properties with pagination and optional name search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PropertySummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<PropertySummaryDto>>>> GetProperties(
        [FromQuery] PropertyQueryParameters query,
        CancellationToken cancellationToken)
    {
        var response = await propertyService.GetAsync(query, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Gets one active property, including amenities, rules, recommendations, emergency contacts, and knowledge articles.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PropertyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PropertyDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PropertyDto>>> GetProperty(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await propertyService.GetByIdAsync(id, cancellationToken);
        return response.Success ? Ok(response) : response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }

    /// <summary>
    /// Creates a property with optional nested setup data.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PropertyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<PropertyDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PropertyDto>>> CreateProperty(
        CreatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await propertyService.CreateAsync(request, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return BadRequest(response);
        }

        return CreatedAtAction(nameof(GetProperty), new { id = response.Data.Id }, response);
    }

    /// <summary>
    /// Updates a property and replaces its nested setup data.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PropertyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PropertyDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PropertyDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PropertyDto>>> UpdateProperty(
        Guid id,
        UpdatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await propertyService.UpdateAsync(id, request, cancellationToken);
        if (response.Success)
        {
            return Ok(response);
        }

        return response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }

    /// <summary>
    /// Soft deletes a property.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProperty(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await propertyService.DeleteAsync(id, cancellationToken);
        return response.Success ? Ok(response) : response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }
}
