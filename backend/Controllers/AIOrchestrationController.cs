using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Temporary authenticated test surface for the StayFlow AI orchestration pipeline.
/// </summary>
[ApiController]
[Route("ai/orchestrate")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("ai-generation")]
[RequireFeature(FeatureKeys.AiConcierge)]
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
        return StatusCode(StatusCodes.Status410Gone,
            ApiResponse<AIOrchestrationResult>.Fail("The legacy AI orchestration endpoint is no longer available."));
    }
}
