using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.PlatformAdmin;
using StayFlow.Api.Models;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize(Policy = OrganizationPolicyNames.PlatformAdmin)]
[Route("api/platform-admin")]
[Produces("application/json")]
public sealed class PlatformAdminController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet("tenants")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PlatformTenantSummaryDto>>>> GetTenants(CancellationToken cancellationToken)
    {
        var subscriptions = dbContext.TenantSubscriptions.AsNoTracking();

        var items = await dbContext.Companies
            .AsNoTracking()
            .Select(company => new PlatformTenantSummaryDto
            {
                CompanyId = company.Id,
                Name = company.Name,
                Status = company.Status,
                SubscriptionStatus = subscriptions
                    .Where(subscription => subscription.CompanyId == company.Id)
                    .OrderByDescending(subscription => subscription.CurrentPeriodStartUtc)
                    .Select(subscription => subscription.Status)
                    .FirstOrDefault(),
                UserCount = dbContext.Users.Count(user => user.CompanyId == company.Id),
                PropertyCount = dbContext.Properties.Count(property => property.CompanyId == company.Id && !property.IsDeleted),
                CreatedAt = company.CreatedAt
            })
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<PlatformTenantSummaryDto>>.Ok(items));
    }

    [HttpGet("subscriptions")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> GetSubscriptions(CancellationToken cancellationToken)
    {
        var items = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => new
            {
                item.CompanyId,
                item.Status,
                item.SubscriptionPlanId,
                item.ExternalSubscriptionId,
                item.CancelAtPeriodEnd,
                item.CurrentPeriodStartUtc,
                item.CurrentPeriodEndUtc,
                item.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(items));
    }

    [HttpGet("usage")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> GetUsage(CancellationToken cancellationToken)
    {
        var usage = await dbContext.UsageRecords
            .AsNoTracking()
            .OrderByDescending(item => item.PeriodStartUtc)
            .Take(500)
            .Select(item => new
            {
                item.CompanyId,
                item.Metric,
                item.QuantityUsed,
                item.PeriodStartUtc,
                item.PeriodEndUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(usage));
    }

    [HttpGet("failed-payments")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> GetFailedPayments(CancellationToken cancellationToken)
    {
        var failed = await dbContext.TenantInvoices
            .AsNoTracking()
            .Where(item => item.FailedAtUtc != null)
            .OrderByDescending(item => item.FailedAtUtc)
            .Take(200)
            .Select(item => new
            {
                item.CompanyId,
                item.ExternalInvoiceId,
                item.AmountDue,
                item.Currency,
                item.FailedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(failed));
    }

    [HttpGet("operational-status")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<PlatformSaasMetricsDto>>> GetOperationalStatus(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var from = now.AddDays(-30);

        var activeTenants = await dbContext.TenantSubscriptions.CountAsync(item => item.Status == SubscriptionStatus.Active.ToStorageValue(), cancellationToken);
        var trialTenants = await dbContext.TenantSubscriptions.CountAsync(item => item.Status == SubscriptionStatus.Trialing.ToStorageValue(), cancellationToken);
        var paidTenants = await dbContext.TenantSubscriptions.CountAsync(item => item.Status == SubscriptionStatus.Active.ToStorageValue() || item.Status == SubscriptionStatus.CancelAtPeriodEnd.ToStorageValue(), cancellationToken);
        var churnEvents = await dbContext.TenantSubscriptions.CountAsync(item => item.EndedAtUtc != null && item.EndedAtUtc >= from, cancellationToken);
        var failedPayments = await dbContext.TenantInvoices.CountAsync(item => item.FailedAtUtc != null && item.FailedAtUtc >= from, cancellationToken);
        var aiUsage = await dbContext.UsageRecords.Where(item => item.Metric == UsageMetric.AiRequests.ToStorageValue() && item.PeriodStartUtc >= from).SumAsync(item => (long?)item.QuantityUsed, cancellationToken) ?? 0;
        var waUsage = await dbContext.UsageRecords.Where(item => item.Metric == UsageMetric.WhatsAppMessages.ToStorageValue() && item.PeriodStartUtc >= from).SumAsync(item => (long?)item.QuantityUsed, cancellationToken) ?? 0;
        var propertyCount = await dbContext.Properties.CountAsync(item => !item.IsDeleted, cancellationToken);
        var userCount = await dbContext.Users.CountAsync(cancellationToken);

        const decimal assumedMrrPerPaidTenant = 99m;
        var mrr = paidTenants * assumedMrrPerPaidTenant;

        var dto = new PlatformSaasMetricsDto
        {
            ActiveTenants = activeTenants,
            TrialTenants = trialTenants,
            PaidTenants = paidTenants,
            MrrEstimate = mrr,
            ArrEstimate = mrr * 12,
            ChurnEventsLast30Days = churnEvents,
            FailedPaymentsLast30Days = failedPayments,
            AiUsageLast30Days = aiUsage,
            WhatsAppUsageLast30Days = waUsage,
            PropertyCount = propertyCount,
            UserCount = userCount,
            DataFreshAtUtc = DateTimeOffset.UtcNow
        };

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "PlatformAdmin",
            EntityId = Guid.Empty,
            Action = "PlatformAdminOperationalStatusViewed",
            Details = "{\"estimatedMetrics\":true}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<PlatformSaasMetricsDto>.Ok(dto));
    }
}