using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize(Policy = OrganizationPolicyNames.PlatformAdmin)]
[Route("api/platform-admin/customer-billing")]
[Produces("application/json")]
public sealed class PlatformAdminCustomerBillingController(
    ICustomerBillingReconciliationService billingService) : ControllerBase
{
    [HttpGet("summary")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<CustomerBillingSummary>>> GetSummary(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? tenantSubscriptionId,
        [FromQuery] Guid? subscriptionPlanId,
        [FromQuery] UsageMetric? metric,
        [FromQuery] CustomerBillableUsageState? billingState,
        [FromQuery] CustomerBillableUsageReason? billingReason,
        CancellationToken cancellationToken)
    {
        if (!TryCreateQuery(
                fromUtc, toUtc, companyId, tenantSubscriptionId, subscriptionPlanId, metric, billingState,
                billingReason, out var query, out var error))
        {
            return BadRequest(ApiResponse<CustomerBillingSummary>.Fail(error));
        }

        var result = await billingService.GetSummaryAsync(query, cancellationToken);
        return Ok(ApiResponse<CustomerBillingSummary>.Ok(result));
    }

    [HttpGet("breakdown")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerBillingBreakdownRow>>>> GetBreakdown(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] string? groupBy,
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? tenantSubscriptionId,
        [FromQuery] Guid? subscriptionPlanId,
        [FromQuery] UsageMetric? metric,
        [FromQuery] CustomerBillableUsageState? billingState,
        [FromQuery] CustomerBillableUsageReason? billingReason,
        CancellationToken cancellationToken)
    {
        if (!TryCreateQuery(
                fromUtc, toUtc, companyId, tenantSubscriptionId, subscriptionPlanId, metric, billingState,
                billingReason, out var query, out var error))
        {
            return BadRequest(ApiResponse<IReadOnlyList<CustomerBillingBreakdownRow>>.Fail(error));
        }

        if (!Enum.TryParse<CustomerBillingGroupBy>(groupBy, true, out var parsedGroupBy)
            || !Enum.IsDefined(parsedGroupBy))
        {
            return BadRequest(ApiResponse<IReadOnlyList<CustomerBillingBreakdownRow>>.Fail(
                "groupBy must be Company, Subscription, Plan, Metric, BillingPeriod, BillingState, BillingReason, or PricingVersion."));
        }

        var result = await billingService.GetBreakdownAsync(query, parsedGroupBy, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CustomerBillingBreakdownRow>>.Ok(result));
    }

    [HttpGet("reconciliation")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<CustomerBillingReconciliation>>> GetReconciliation(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? tenantSubscriptionId,
        [FromQuery] Guid? subscriptionPlanId,
        [FromQuery] UsageMetric? metric,
        [FromQuery] CustomerBillableUsageState? billingState,
        [FromQuery] CustomerBillableUsageReason? billingReason,
        CancellationToken cancellationToken)
    {
        if (!TryCreateQuery(
                fromUtc, toUtc, companyId, tenantSubscriptionId, subscriptionPlanId, metric, billingState,
                billingReason, out var query, out var error))
        {
            return BadRequest(ApiResponse<CustomerBillingReconciliation>.Fail(error));
        }

        var result = await billingService.GetReconciliationAsync(query, cancellationToken);
        return Ok(ApiResponse<CustomerBillingReconciliation>.Ok(result));
    }

    private static bool TryCreateQuery(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? companyId,
        Guid? tenantSubscriptionId,
        Guid? subscriptionPlanId,
        UsageMetric? metric,
        CustomerBillableUsageState? billingState,
        CustomerBillableUsageReason? billingReason,
        [NotNullWhen(true)] out CustomerBillingQuery? query,
        out string error)
    {
        query = null;
        if (fromUtc is null || toUtc is null)
        {
            error = "fromUtc and toUtc are required.";
            return false;
        }

        try
        {
            query = new CustomerBillingQuery(
                fromUtc.Value,
                toUtc.Value,
                companyId,
                tenantSubscriptionId,
                subscriptionPlanId,
                metric,
                billingState,
                billingReason);
            CustomerBillingReconciliationService.ValidateQuery(query);
            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            query = null;
            return false;
        }
    }
}
