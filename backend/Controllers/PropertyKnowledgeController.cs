using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.PropertyKnowledge;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Route("properties/{propertyId:guid}/knowledge")]
[Produces("application/json")]
[Authorize]
public sealed class PropertyKnowledgeController(IPropertyKnowledgeService propertyKnowledgeService) : ControllerBase
{
    [HttpGet]
    [RequiresPermission("properties.read")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>>> GetKnowledge(
        Guid propertyId,
        [FromQuery] PropertyKnowledgeListQuery query,
        CancellationToken cancellationToken)
    {
        var response = await propertyKnowledgeService.GetAsync(propertyId, query, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpGet("{knowledgeId:guid}")]
    [RequiresPermission("properties.read")]
    [ProducesResponseType(typeof(ApiResponse<PropertyKnowledgeDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PropertyKnowledgeDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PropertyKnowledgeDetailResponse>>> GetKnowledgeById(
        Guid propertyId,
        Guid knowledgeId,
        CancellationToken cancellationToken)
    {
        var response = await propertyKnowledgeService.GetByIdAsync(propertyId, knowledgeId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost]
    [RequiresPermission("properties.manage")]
    [ProducesResponseType(typeof(ApiResponse<PropertyKnowledgeDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<PropertyKnowledgeDetailResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PropertyKnowledgeDetailResponse>>> CreateKnowledge(
        Guid propertyId,
        [FromBody] CreatePropertyKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        var response = await propertyKnowledgeService.CreateAsync(propertyId, request, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return ToFailureResult(response);
        }

        return CreatedAtAction(nameof(GetKnowledgeById), new { propertyId, knowledgeId = response.Data.Id }, response);
    }

    [HttpPut("{knowledgeId:guid}")]
    [RequiresPermission("properties.manage")]
    [ProducesResponseType(typeof(ApiResponse<PropertyKnowledgeDetailResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PropertyKnowledgeDetailResponse>>> UpdateKnowledge(
        Guid propertyId,
        Guid knowledgeId,
        [FromBody] UpdatePropertyKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        var response = await propertyKnowledgeService.UpdateAsync(propertyId, knowledgeId, request, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{knowledgeId:guid}/approve")]
    [RequiresPermission("properties.approve")]
    public async Task<ActionResult<ApiResponse<PropertyKnowledgeDetailResponse>>> ApproveKnowledge(
        Guid propertyId,
        Guid knowledgeId,
        CancellationToken cancellationToken)
    {
        var response = await propertyKnowledgeService.ApproveAsync(propertyId, knowledgeId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{knowledgeId:guid}/unapprove")]
    [RequiresPermission("properties.approve")]
    public async Task<ActionResult<ApiResponse<PropertyKnowledgeDetailResponse>>> UnapproveKnowledge(
        Guid propertyId,
        Guid knowledgeId,
        CancellationToken cancellationToken)
    {
        var response = await propertyKnowledgeService.UnapproveAsync(propertyId, knowledgeId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{knowledgeId:guid}/activate")]
    [RequiresPermission("properties.manage")]
    public async Task<ActionResult<ApiResponse<PropertyKnowledgeDetailResponse>>> ActivateKnowledge(
        Guid propertyId,
        Guid knowledgeId,
        CancellationToken cancellationToken)
    {
        var response = await propertyKnowledgeService.ActivateAsync(propertyId, knowledgeId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpPost("{knowledgeId:guid}/deactivate")]
    [RequiresPermission("properties.manage")]
    public async Task<ActionResult<ApiResponse<PropertyKnowledgeDetailResponse>>> DeactivateKnowledge(
        Guid propertyId,
        Guid knowledgeId,
        CancellationToken cancellationToken)
    {
        var response = await propertyKnowledgeService.DeactivateAsync(propertyId, knowledgeId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    [HttpDelete("{knowledgeId:guid}")]
    [RequiresPermission("properties.manage")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteKnowledge(
        Guid propertyId,
        Guid knowledgeId,
        CancellationToken cancellationToken)
    {
        var response = await propertyKnowledgeService.DeleteAsync(propertyId, knowledgeId, cancellationToken);
        return response.Success ? Ok(response) : ToFailureResult(response);
    }

    private ActionResult<ApiResponse<T>> ToFailureResult<T>(ApiResponse<T> response)
    {
        return response.Errors.Count > 0 ? BadRequest(response) : NotFound(response);
    }
}