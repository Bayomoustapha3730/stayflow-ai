using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Plans;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("organization/current/plan")]
[Produces("application/json")]
public sealed class OrganizationPlanController(
    ICurrentTenantContext tenantContext,
    ISubscriptionEntitlementService subscriptionEntitlementService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    [ProducesResponseType(typeof(ApiResponse<CurrentPlanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CurrentPlanResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CurrentPlanResponse>>> GetCurrentPlan(CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var error))
        {
            return BadRequest(ApiResponse<CurrentPlanResponse>.Fail(error));
        }

        var snapshot = await subscriptionEntitlementService.GetCurrentSnapshotAsync(companyId, cancellationToken);
        return Ok(ApiResponse<CurrentPlanResponse>.Ok(MapResponse(snapshot)));
    }

    [HttpGet("usage")]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<QuotaUsageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<QuotaUsageDto>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<QuotaUsageDto>>>> GetUsage(CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var error))
        {
            return BadRequest(ApiResponse<IReadOnlyCollection<QuotaUsageDto>>.Fail(error));
        }

        var snapshot = await subscriptionEntitlementService.GetCurrentSnapshotAsync(companyId, cancellationToken);
        var quotas = snapshot.Quotas.Select(MapQuota).ToList();
        return Ok(ApiResponse<IReadOnlyCollection<QuotaUsageDto>>.Ok(quotas));
    }

    [HttpPut]
    [Authorize(Policy = OrganizationPolicyNames.Owner)]
    [ProducesResponseType(typeof(ApiResponse<CurrentPlanResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CurrentPlanResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CurrentPlanResponse>>> UpdateCurrentPlan(
        [FromBody] UpdateCurrentPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var error))
        {
            return BadRequest(ApiResponse<CurrentPlanResponse>.Fail(error));
        }

        var snapshot = await subscriptionEntitlementService.UpdatePlanAsync(companyId, request.PlanId, request.PlanName, request.Notes, cancellationToken);
        return Ok(ApiResponse<CurrentPlanResponse>.Ok(MapResponse(snapshot), "Plan updated successfully."));
    }

    private bool TryGetCompanyId(out Guid companyId, out string error)
    {
        companyId = tenantContext.CompanyId ?? Guid.Empty;
        if (!tenantContext.IsAuthenticated || companyId == Guid.Empty)
        {
            error = "Authenticated tenant context is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static CurrentPlanResponse MapResponse(SubscriptionSnapshot snapshot)
    {
        return new CurrentPlanResponse
        {
            CompanyId = snapshot.CompanyId,
            SubscriptionId = snapshot.SubscriptionId,
            PlanId = snapshot.PlanId,
            PlanName = snapshot.PlanName,
            PlanDisplayName = snapshot.PlanDisplayName,
            SubscriptionStatus = snapshot.SubscriptionStatus,
            IsEnterprise = snapshot.IsEnterprise,
            CurrentPeriodStartUtc = snapshot.CurrentPeriodStartUtc,
            CurrentPeriodEndUtc = snapshot.CurrentPeriodEndUtc,
            Features = snapshot.Features
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new FeatureEntitlementDto
                {
                    Key = item.Key,
                    IsEnabled = item.IsEnabled
                })
                .ToList(),
            Quotas = snapshot.Quotas
                .OrderBy(item => item.EntitlementKey, StringComparer.Ordinal)
                .Select(MapQuota)
                .ToList()
        };
    }

    private static QuotaUsageDto MapQuota(QuotaSnapshot quota)
    {
        return new QuotaUsageDto
        {
            Metric = quota.Metric.ToStorageValue(),
            EntitlementKey = quota.EntitlementKey,
            Limit = quota.Limit,
            Used = quota.Used,
            Remaining = quota.Remaining,
            IsUnlimited = quota.IsUnlimited,
            Unit = quota.Unit,
            PeriodStartUtc = quota.PeriodStartUtc,
            PeriodEndUtc = quota.PeriodEndUtc
        };
    }
}