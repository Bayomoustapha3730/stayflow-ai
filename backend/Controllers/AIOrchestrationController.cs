using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Temporary authenticated test surface for the StayFlow AI orchestration pipeline.
/// </summary>
[ApiController]
[Route("ai/orchestrate")]
[Produces("application/json")]
[Authorize]
public sealed class AIOrchestrationController(IAIOrchestrator aiOrchestrator) : ControllerBase
{
    /// <summary>
    /// Runs the deterministic AI orchestration pipeline for an authenticated tenant.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AIOrchestrationResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AIOrchestrationResult>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AIOrchestrationResult>>> Orchestrate(
        [FromBody] AIOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GuestMessage))
        {
            return BadRequest(ApiResponse<AIOrchestrationResult>.Fail("Guest message is required.", ["GuestMessage is required."]));
        }

        var result = await aiOrchestrator.ProcessAsync(request, cancellationToken);
        return Ok(ApiResponse<AIOrchestrationResult>.Ok(result, "AI orchestration completed."));
    }
}
