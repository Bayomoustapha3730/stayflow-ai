using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Companies;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Manages companies that own or operate StayFlow AI properties.
/// </summary>
[ApiController]
[Route("companies")]
[Produces("application/json")]
public sealed class CompaniesController(ICompanyService companyService) : ControllerBase
{
    /// <summary>
    /// Gets active companies with pagination and optional name search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CompanyDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<CompanyDto>>>> GetCompanies(
        [FromQuery] CompanyQueryParameters query,
        CancellationToken cancellationToken)
    {
        var response = await companyService.GetAsync(query, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Gets one active company by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompanyDto>>> GetCompany(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await companyService.GetByIdAsync(id, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    /// <summary>
    /// Creates a company.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CompanyDto>>> CreateCompany(
        [FromBody] CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await companyService.CreateAsync(request, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return BadRequest(response);
        }

        return CreatedAtAction(nameof(GetCompany), new { id = response.Data.Id }, response);
    }

    /// <summary>
    /// Updates a company.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompanyDto>>> UpdateCompany(
        Guid id,
        [FromBody] UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await companyService.UpdateAsync(id, request, cancellationToken);
        if (response.Success)
        {
            return Ok(response);
        }

        return response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }

    /// <summary>
    /// Soft deletes a company.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCompany(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await companyService.DeleteAsync(id, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }
}
