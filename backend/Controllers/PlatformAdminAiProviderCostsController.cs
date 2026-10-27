using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize(Policy = OrganizationPolicyNames.PlatformAdmin)]
[Route("api/platform-admin/ai-provider-costs")]
[Produces("application/json")]
public sealed class PlatformAdminAiProviderCostsController(
    IAiProviderCostQueryService costQueryService) : ControllerBase
{
    [HttpGet("summary")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<AiProviderCostSummary>>> GetSummary(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] Guid? companyId,
        [FromQuery] string? provider,
        [FromQuery] string? resolvedModel,
        CancellationToken cancellationToken)
    {
        if (!TryCreateQuery(fromUtc, toUtc, companyId, provider, resolvedModel, out var query, out var error))
        {
            return BadRequest(ApiResponse<AiProviderCostSummary>.Fail(error));
        }

        var summary = await costQueryService.GetSummaryAsync(query, cancellationToken);
        return Ok(ApiResponse<AiProviderCostSummary>.Ok(summary));
    }

    [HttpGet("breakdown")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiProviderCostBreakdownRow>>>> GetBreakdown(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] string? groupBy,
        [FromQuery] Guid? companyId,
        [FromQuery] string? provider,
        [FromQuery] string? resolvedModel,
        CancellationToken cancellationToken)
    {
        if (!TryCreateQuery(fromUtc, toUtc, companyId, provider, resolvedModel, out var query, out var error))
        {
            return BadRequest(ApiResponse<IReadOnlyList<AiProviderCostBreakdownRow>>.Fail(error));
        }

        if (!Enum.TryParse<AiProviderCostGroupBy>(groupBy, true, out var parsedGroupBy)
            || !Enum.IsDefined(parsedGroupBy))
        {
            return BadRequest(ApiResponse<IReadOnlyList<AiProviderCostBreakdownRow>>.Fail(
                "groupBy must be Provider, ResolvedModel, Company, UtcDay, UtcMonth, or OuterOperationType."));
        }

        var result = await costQueryService.GetBreakdownAsync(query, parsedGroupBy, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiProviderCostBreakdownRow>>.Ok(result));
    }

    private static bool TryCreateQuery(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? companyId,
        string? provider,
        string? resolvedModel,
        [NotNullWhen(true)] out AiProviderCostQuery? query,
        out string error)
    {
        query = null;
        if (fromUtc is null || toUtc is null)
        {
            error = "fromUtc and toUtc are required.";
            return false;
        }

        var from = fromUtc.Value.ToUniversalTime();
        var to = toUtc.Value.ToUniversalTime();
        if (from >= to)
        {
            error = "fromUtc must be earlier than toUtc.";
            return false;
        }

        if (to - from > TimeSpan.FromDays(366))
        {
            error = "Reporting windows cannot exceed 366 days.";
            return false;
        }

        query = new AiProviderCostQuery(from, to, companyId, provider, resolvedModel);
        error = string.Empty;
        return true;
    }
}
