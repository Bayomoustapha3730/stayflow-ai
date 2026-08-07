using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.PlatformAdmin;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize(Policy = OrganizationPolicyNames.PlatformAdmin)]
[Route("api/platform-admin")]
[Produces("application/json")]
public sealed class PlatformAdminController(
    ApplicationDbContext dbContext,
    ISubscriptionEntitlementService subscriptionEntitlementService,
    IConfiguration configuration,
    IOptions<AIProviderOptions> aiProviderOptions,
    IOptions<OpenAIOptions> openAiOptions,
    IHostEnvironment hostEnvironment) : ControllerBase
{
    private static readonly HashSet<string> ManagedTenantStatuses =
    [
        "Active",
        "Suspended",
        "Archived"
    ];

    [HttpGet("tenants")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<PlatformTenantPagedResultDto>>> GetTenants(
        [FromQuery] PlatformTenantQueryDto query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var normalizedSearch = NormalizeOptional(query.Search);
        var normalizedStatus = NormalizeOptional(query.Status);

        var subscriptions = dbContext.TenantSubscriptions.AsNoTracking();
        var companiesQuery = dbContext.Companies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var searchValue = normalizedSearch.ToUpperInvariant();
            companiesQuery = companiesQuery.Where(company =>
                company.Name.ToUpper().Contains(searchValue)
                || company.Slug.ToUpper().Contains(searchValue)
                || company.Email.ToUpper().Contains(searchValue));
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            companiesQuery = companiesQuery.Where(company => company.Status.ToUpper() == normalizedStatus.ToUpperInvariant());
        }

        var totalCount = await companiesQuery.CountAsync(cancellationToken);

        var items = await companiesQuery
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
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        await AddAuditAsync("PlatformTenantSearch", Guid.Empty, new
        {
            normalizedSearch,
            normalizedStatus,
            page,
            pageSize,
            totalCount
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<PlatformTenantPagedResultDto>.Ok(new PlatformTenantPagedResultDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        }));
    }

    [HttpGet("tenants/{companyId:guid}")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<PlatformTenantDetailDto>>> GetTenantById(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return NotFound(ApiResponse<PlatformTenantDetailDto>.Fail("Tenant was not found."));
        }

        var latestSubscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var planName = latestSubscription is null
            ? null
            : await dbContext.SubscriptionPlans
                .AsNoTracking()
                .Where(item => item.Id == latestSubscription.SubscriptionPlanId)
                .Select(item => item.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var aiUsage = await dbContext.UsageRecords
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.Metric == UsageMetric.AiRequests.ToStorageValue()
                && item.PeriodStartUtc >= from)
            .SumAsync(item => (long?)item.QuantityUsed, cancellationToken) ?? 0;
        var apiUsage = await dbContext.UsageRecords
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.Metric == UsageMetric.ApiRequests.ToStorageValue()
                && item.PeriodStartUtc >= from)
            .SumAsync(item => (long?)item.QuantityUsed, cancellationToken) ?? 0;

        var dto = new PlatformTenantDetailDto
        {
            CompanyId = company.Id,
            Name = company.Name,
            Slug = company.Slug,
            Status = company.Status,
            IsActive = company.IsActive,
            SubscriptionStatus = latestSubscription?.Status,
            CurrentPlanName = planName,
            UserCount = await dbContext.Users.CountAsync(item => item.CompanyId == companyId, cancellationToken),
            PropertyCount = await dbContext.Properties.CountAsync(item => item.CompanyId == companyId && !item.IsDeleted, cancellationToken),
            ConversationCount = await dbContext.Conversations.CountAsync(item => item.CompanyId == companyId && !item.IsDeleted, cancellationToken),
            AiUsageLast30Days = aiUsage,
            ApiUsageLast30Days = apiUsage,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };

        await AddAuditAsync("PlatformTenantViewed", companyId, null, cancellationToken);
        return Ok(ApiResponse<PlatformTenantDetailDto>.Ok(dto));
    }

    [HttpPost("tenants/{companyId:guid}/suspend")]
    [RequiresPermission("platform.admin")]
    public Task<ActionResult<ApiResponse<PlatformTenantDetailDto>>> SuspendTenant(Guid companyId, [FromBody] PlatformTenantActionRequest request, CancellationToken cancellationToken)
        => SetTenantStatusAsync(companyId, "Suspended", false, "PlatformTenantSuspended", request.Reason, cancellationToken);

    [HttpPost("tenants/{companyId:guid}/reactivate")]
    [RequiresPermission("platform.admin")]
    public Task<ActionResult<ApiResponse<PlatformTenantDetailDto>>> ReactivateTenant(Guid companyId, [FromBody] PlatformTenantActionRequest request, CancellationToken cancellationToken)
        => SetTenantStatusAsync(companyId, "Active", true, "PlatformTenantReactivated", request.Reason, cancellationToken);

    [HttpPost("tenants/{companyId:guid}/archive")]
    [RequiresPermission("platform.admin")]
    public Task<ActionResult<ApiResponse<PlatformTenantDetailDto>>> ArchiveTenant(Guid companyId, [FromBody] PlatformTenantActionRequest request, CancellationToken cancellationToken)
        => SetTenantStatusAsync(companyId, "Archived", false, "PlatformTenantArchived", request.Reason, cancellationToken);

    [HttpPost("tenants/{companyId:guid}/restore")]
    [RequiresPermission("platform.admin")]
    public Task<ActionResult<ApiResponse<PlatformTenantDetailDto>>> RestoreTenant(Guid companyId, [FromBody] PlatformTenantActionRequest request, CancellationToken cancellationToken)
        => SetTenantStatusAsync(companyId, "Active", true, "PlatformTenantRestored", request.Reason, cancellationToken);

    [HttpGet("tenants/{companyId:guid}/audit")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PlatformTenantLifecycleAuditDto>>>> GetTenantLifecycleAudit(Guid companyId, CancellationToken cancellationToken)
    {
        var items = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(item => item.EntityId == companyId
                && (item.Action.StartsWith("PlatformTenant")
                    || item.Action.StartsWith("SupportImpersonation")
                    || item.Action == "PlatformTenantRepairExecuted"
                    || item.Action == "PlatformTenantSubscriptionSynchronized"))
            .OrderByDescending(item => item.CreatedAt)
            .Take(250)
            .Select(item => new PlatformTenantLifecycleAuditDto
            {
                AuditLogId = item.Id,
                Action = item.Action,
                Details = item.Details,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<PlatformTenantLifecycleAuditDto>>.Ok(items));
    }

    [HttpGet("tenants/{companyId:guid}/health")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<PlatformOrganizationHealthDto>>> GetTenantHealth(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return NotFound(ApiResponse<PlatformOrganizationHealthDto>.Fail("Tenant was not found."));
        }

        var activeOwnerOrAdminCount = await dbContext.OrganizationMembers
            .AsNoTracking()
            .CountAsync(item => item.CompanyId == companyId
                && item.Status == OrganizationMemberStatus.Active.ToStorageValue()
                && (item.Role == OrganizationRole.Owner.ToStorageValue() || item.Role == OrganizationRole.Administrator.ToStorageValue()), cancellationToken);

        var openConversations = await dbContext.Conversations
            .AsNoTracking()
            .CountAsync(item => item.CompanyId == companyId
                && !item.IsDeleted
                && item.Status == ConversationStatus.Open, cancellationToken);

        var overdueActionCount = await dbContext.PendingConciergeActions
            .AsNoTracking()
            .CountAsync(item => item.CompanyId == companyId
                && item.Status == PendingConciergeActionStatus.AwaitingHostApproval
                && item.ExpiresAt < DateTimeOffset.UtcNow, cancellationToken);

        var signals = new List<string>();
        if (!company.IsActive || !string.Equals(company.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            signals.Add("Tenant is not active.");
        }
        if (activeOwnerOrAdminCount == 0)
        {
            signals.Add("No active owner or administrator is assigned.");
        }
        if (overdueActionCount > 0)
        {
            signals.Add("Pending concierge actions are overdue.");
        }

        var dto = new PlatformOrganizationHealthDto
        {
            CompanyId = companyId,
            OrganizationName = company.Name,
            Status = company.Status,
            IsActive = company.IsActive,
            ActiveUserCount = await dbContext.Users.CountAsync(item => item.CompanyId == companyId && item.IsActive, cancellationToken),
            ActiveOwnerOrAdminCount = activeOwnerOrAdminCount,
            ActivePropertyCount = await dbContext.Properties.CountAsync(item => item.CompanyId == companyId && item.IsActive && !item.IsDeleted, cancellationToken),
            OpenConversations = openConversations,
            OverdueActionCount = overdueActionCount,
            HasBlockingIssues = signals.Count > 0,
            HealthSignals = signals
        };

        await AddAuditAsync("PlatformTenantHealthViewed", companyId, null, cancellationToken);
        return Ok(ApiResponse<PlatformOrganizationHealthDto>.Ok(dto));
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
    public async Task<ActionResult<ApiResponse<PlatformUsageOverviewDto>>> GetUsage(CancellationToken cancellationToken)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-30);

        var usage = await dbContext.UsageRecords
            .AsNoTracking()
            .Where(item => item.PeriodStartUtc >= from)
            .ToListAsync(cancellationToken);

        var dto = new PlatformUsageOverviewDto
        {
            ApiRequestsLast30Days = usage.Where(item => item.Metric == UsageMetric.ApiRequests.ToStorageValue()).Sum(item => item.QuantityUsed),
            AiRequestsLast30Days = usage.Where(item => item.Metric == UsageMetric.AiRequests.ToStorageValue()).Sum(item => item.QuantityUsed),
            AiTokensLast30Days = usage.Where(item => item.Metric == UsageMetric.AiTokens.ToStorageValue()).Sum(item => item.QuantityUsed),
            WhatsAppMessagesLast30Days = usage.Where(item => item.Metric == UsageMetric.WhatsAppMessages.ToStorageValue()).Sum(item => item.QuantityUsed),
            ReservationsLast30Days = usage.Where(item => item.Metric == UsageMetric.Reservations.ToStorageValue()).Sum(item => item.QuantityUsed),
            FileUploadsLast30Days = usage.Where(item => item.Metric == UsageMetric.FileUploads.ToStorageValue()).Sum(item => item.QuantityUsed),
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };

        return Ok(ApiResponse<PlatformUsageOverviewDto>.Ok(dto));
    }

    [HttpGet("feature-flags")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PlatformFeatureFlagDto>>>> GetFeatureFlags(CancellationToken cancellationToken)
    {
        var items = await dbContext.PlanEntitlements
            .AsNoTracking()
            .Join(dbContext.SubscriptionPlans.AsNoTracking(), entitlement => entitlement.SubscriptionPlanId, plan => plan.Id,
                (entitlement, plan) => new PlatformFeatureFlagDto
                {
                    PlanName = plan.DisplayName,
                    PlanId = plan.Id,
                    Key = entitlement.Key,
                    IsEnabled = entitlement.IsEnabled,
                    IsUnlimited = entitlement.IsUnlimited,
                    QuotaLimit = entitlement.QuotaLimit,
                    Unit = entitlement.Unit,
                    Notes = entitlement.Notes
                })
            .OrderBy(item => item.PlanName)
            .ThenBy(item => item.Key)
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyCollection<PlatformFeatureFlagDto>>.Ok(items));
    }

    [HttpPut("feature-flags/{planId:guid}/{flagKey}")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<PlatformFeatureFlagDto>>> UpdateFeatureFlag(
        Guid planId,
        string flagKey,
        [FromBody] PlatformUpdateFeatureFlagRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeOptional(flagKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return BadRequest(ApiResponse<PlatformFeatureFlagDto>.Fail("Feature flag key is required."));
        }

        var entitlement = await dbContext.PlanEntitlements
            .FirstOrDefaultAsync(item => item.SubscriptionPlanId == planId && item.Key == normalizedKey, cancellationToken);
        if (entitlement is null)
        {
            return NotFound(ApiResponse<PlatformFeatureFlagDto>.Fail("Feature flag was not found for the selected plan."));
        }

        entitlement.IsEnabled = request.IsEnabled;
        entitlement.QuotaLimit = request.QuotaLimit;
        entitlement.IsUnlimited = request.IsUnlimited ?? entitlement.IsUnlimited;
        entitlement.Unit = NormalizeOptional(request.Unit);
        entitlement.Notes = NormalizeOptional(request.Notes);

        await AddAuditAsync("PlatformFeatureFlagUpdated", planId, new
        {
            entitlement.Key,
            entitlement.IsEnabled,
            entitlement.QuotaLimit,
            entitlement.IsUnlimited
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var planName = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(item => item.Id == planId)
            .Select(item => item.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

        return Ok(ApiResponse<PlatformFeatureFlagDto>.Ok(new PlatformFeatureFlagDto
        {
            PlanName = planName,
            PlanId = planId,
            Key = entitlement.Key,
            IsEnabled = entitlement.IsEnabled,
            IsUnlimited = entitlement.IsUnlimited,
            QuotaLimit = entitlement.QuotaLimit,
            Unit = entitlement.Unit,
            Notes = entitlement.Notes
        }));
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
        var from = DateTimeOffset.UtcNow.AddDays(-30);

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

        await AddAuditAsync("PlatformAdminOperationalStatusViewed", Guid.Empty, new { estimatedMetrics = true }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<PlatformSaasMetricsDto>.Ok(dto));
    }

    [HttpGet("operations/metrics")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<PlatformOperationalMetricsDto>>> GetOperationalMetrics(CancellationToken cancellationToken)
    {
        var from30 = DateTimeOffset.UtcNow.AddDays(-30);
        var from24 = DateTimeOffset.UtcNow.AddHours(-24);

        var apiRequests = await dbContext.UsageRecords
            .AsNoTracking()
            .Where(item => item.Metric == UsageMetric.ApiRequests.ToStorageValue() && item.PeriodStartUtc >= from30)
            .SumAsync(item => (long?)item.QuantityUsed, cancellationToken) ?? 0;
        var signalrEvents = await dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(item => item.Action.Contains("Conversation") && item.CreatedAt >= from30, cancellationToken);
        var aiRequests = await dbContext.UsageRecords
            .AsNoTracking()
            .Where(item => item.Metric == UsageMetric.AiRequests.ToStorageValue() && item.PeriodStartUtc >= from30)
            .SumAsync(item => (long?)item.QuantityUsed, cancellationToken) ?? 0;
        var billingWebhooks = await dbContext.BillingWebhookEvents
            .AsNoTracking()
            .CountAsync(item => item.ProcessedAtUtc >= from30, cancellationToken);
        var emailEvents = await dbContext.EmailVerificationTokens
            .AsNoTracking()
            .CountAsync(item => item.CreatedAt >= from30, cancellationToken)
            + await dbContext.PasswordResetTokens
                .AsNoTracking()
                .CountAsync(item => item.CreatedAt >= from30, cancellationToken);
        var waMessages = await dbContext.UsageRecords
            .AsNoTracking()
            .Where(item => item.Metric == UsageMetric.WhatsAppMessages.ToStorageValue() && item.PeriodStartUtc >= from30)
            .SumAsync(item => (long?)item.QuantityUsed, cancellationToken) ?? 0;
        var backgroundRetries = await dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(item => item.Action.Contains("Retry") && item.CreatedAt >= from30, cancellationToken);
        var healthIssues = await dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(item => item.Action.Contains("Health")
                && item.Action.Contains("Failed")
                && item.CreatedAt >= from24, cancellationToken);

        var dto = new PlatformOperationalMetricsDto
        {
            ApiRequestsLast30Days = apiRequests,
            SignalREventsLast30Days = signalrEvents,
            AiRequestsLast30Days = aiRequests,
            BillingWebhookEventsLast30Days = billingWebhooks,
            EmailEventsLast30Days = emailEvents,
            WhatsAppMessagesLast30Days = waMessages,
            BackgroundJobRetriesLast30Days = backgroundRetries,
            DatabaseHealthScore = 100,
            QueueDepthEstimate = -1,
            HealthCheckIssuesLast24Hours = healthIssues,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };

        return Ok(ApiResponse<PlatformOperationalMetricsDto>.Ok(dto));
    }

    [HttpGet("operations/background-jobs")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PlatformBackgroundJobStatusDto>>>> GetBackgroundJobStatus(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var from = now.AddHours(-24);

        var failedWebhookJobs = await dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(item => item.Action.Contains("Webhook")
                && item.Action.Contains("Failed")
                && item.CreatedAt >= from, cancellationToken);

        var items = new List<PlatformBackgroundJobStatusDto>
        {
            new()
            {
                JobName = "WhatsAppWebhookBackgroundService",
                Status = failedWebhookJobs > 0 ? "Warning" : "Healthy",
                LastObservedAtUtc = now,
                FailureCountLast24Hours = failedWebhookJobs
            }
        };

        return Ok(ApiResponse<IReadOnlyCollection<PlatformBackgroundJobStatusDto>>.Ok(items));
    }

    [HttpGet("operations/webhooks")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PlatformWebhookMonitoringDto>>>> GetWebhookMonitoring(CancellationToken cancellationToken)
    {
        var from = DateTimeOffset.UtcNow.AddHours(-24);
        var events = await dbContext.BillingWebhookEvents
            .AsNoTracking()
            .Where(item => item.ProcessedAtUtc >= from)
            .ToListAsync(cancellationToken);

        var item = new PlatformWebhookMonitoringDto
        {
            Provider = "Stripe",
            TotalEventsLast24Hours = events.Count,
            DuplicatesLast24Hours = events.Count(entry => entry.WasDuplicate),
            FailedInvoiceEventsLast24Hours = events.Count(entry => string.Equals(entry.EventType, "invoice.payment_failed", StringComparison.OrdinalIgnoreCase)),
            LatestProcessedAtUtc = events.OrderByDescending(entry => entry.ProcessedAtUtc).Select(entry => (DateTimeOffset?)entry.ProcessedAtUtc).FirstOrDefault()
        };

        return Ok(ApiResponse<IReadOnlyCollection<PlatformWebhookMonitoringDto>>.Ok([item]));
    }

    [HttpGet("operations/queues")]
    [RequiresPermission("platform.admin")]
    public ActionResult<ApiResponse<IReadOnlyCollection<PlatformQueueMonitoringDto>>> GetQueueMonitoring()
    {
        var items = new List<PlatformQueueMonitoringDto>
        {
            new()
            {
                QueueName = "WhatsAppWebhookQueue",
                DepthEstimate = -1,
                Notes = "In-memory unbounded channel does not expose depth. -1 indicates unknown depth."
            }
        };

        return Ok(ApiResponse<IReadOnlyCollection<PlatformQueueMonitoringDto>>.Ok(items));
    }

    [HttpGet("operations/email-delivery")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<PlatformEmailDeliveryMonitoringDto>>> GetEmailDeliveryMonitoring(CancellationToken cancellationToken)
    {
        var from = DateTimeOffset.UtcNow.AddHours(-24);

        var passwordResetIssued = await dbContext.PasswordResetTokens
            .AsNoTracking()
            .CountAsync(item => item.CreatedAt >= from, cancellationToken);
        var verificationIssued = await dbContext.EmailVerificationTokens
            .AsNoTracking()
            .CountAsync(item => item.CreatedAt >= from, cancellationToken);
        var expiredTokens = await dbContext.EmailVerificationTokens
            .AsNoTracking()
            .CountAsync(item => item.ExpiresAt <= DateTimeOffset.UtcNow && item.CreatedAt >= from, cancellationToken);

        return Ok(ApiResponse<PlatformEmailDeliveryMonitoringDto>.Ok(new PlatformEmailDeliveryMonitoringDto
        {
            PasswordResetIssuedLast24Hours = passwordResetIssued,
            EmailVerificationIssuedLast24Hours = verificationIssued,
            ExpiredEmailTokensLast24Hours = expiredTokens,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        }));
    }

    [HttpGet("providers/health")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PlatformProviderHealthDto>>>> GetProviderHealth(CancellationToken cancellationToken)
    {
        var from = DateTimeOffset.UtcNow.AddHours(-24);
        var billingRecentFailures = await dbContext.TenantInvoices
            .AsNoTracking()
            .CountAsync(item => item.FailedAtUtc != null && item.FailedAtUtc >= from, cancellationToken);

        var items = new List<PlatformProviderHealthDto>
        {
            new()
            {
                Provider = "AI",
                Status = string.Equals(aiProviderOptions.Value.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase)
                    ? (string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey) ? "Degraded" : "Healthy")
                    : "Development",
                Message = string.Equals(aiProviderOptions.Value.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase)
                    ? (string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey)
                        ? "OpenAI selected but API key is not configured."
                        : "OpenAI configuration is present.")
                    : "Deterministic development provider mode.",
                CheckedAtUtc = DateTimeOffset.UtcNow
            },
            new()
            {
                Provider = "WhatsApp",
                Status = await dbContext.WhatsAppIntegrations.AsNoTracking().AnyAsync(item => item.IsActive, cancellationToken) ? "Healthy" : "Degraded",
                Message = "Health is inferred from active integration presence and per-tenant checks.",
                CheckedAtUtc = DateTimeOffset.UtcNow
            },
            new()
            {
                Provider = "Billing",
                Status = billingRecentFailures > 0 ? "Warning" : "Healthy",
                Message = billingRecentFailures > 0 ? "Recent failed invoices detected." : "No recent failed invoices.",
                CheckedAtUtc = DateTimeOffset.UtcNow
            }
        };

        return Ok(ApiResponse<IReadOnlyCollection<PlatformProviderHealthDto>>.Ok(items));
    }

    [HttpGet("billing/health")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<PlatformBillingHealthDto>>> GetBillingHealth(CancellationToken cancellationToken)
    {
        var from30 = DateTimeOffset.UtcNow.AddDays(-30);
        var from24 = DateTimeOffset.UtcNow.AddHours(-24);

        return Ok(ApiResponse<PlatformBillingHealthDto>.Ok(new PlatformBillingHealthDto
        {
            ActiveSubscriptions = await dbContext.TenantSubscriptions.CountAsync(item => item.Status == SubscriptionStatus.Active.ToStorageValue(), cancellationToken),
            PastDueSubscriptions = await dbContext.TenantSubscriptions.CountAsync(item => item.Status == SubscriptionStatus.PastDue.ToStorageValue(), cancellationToken),
            FailedInvoicesLast30Days = await dbContext.TenantInvoices.CountAsync(item => item.FailedAtUtc != null && item.FailedAtUtc >= from30, cancellationToken),
            WebhookEventsLast24Hours = await dbContext.BillingWebhookEvents.CountAsync(item => item.ProcessedAtUtc >= from24, cancellationToken),
            GeneratedAtUtc = DateTimeOffset.UtcNow
        }));
    }

    [HttpPost("subscriptions/{companyId:guid}/synchronize")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<object>>> SynchronizeSubscription(Guid companyId, [FromBody] PlatformSubscriptionSyncRequest request, CancellationToken cancellationToken)
    {
        var snapshot = await subscriptionEntitlementService.GetCurrentSnapshotAsync(companyId, cancellationToken);
        await AddAuditAsync("PlatformTenantSubscriptionSynchronized", companyId, new
        {
            request.Reason,
            snapshot.SubscriptionId,
            snapshot.PlanName,
            snapshot.SubscriptionStatus
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new
        {
            snapshot.CompanyId,
            snapshot.SubscriptionId,
            snapshot.PlanName,
            snapshot.SubscriptionStatus
        }));
    }

    [HttpPost("tenants/{companyId:guid}/repair")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<object>>> RepairTenant(Guid companyId, [FromBody] PlatformTenantRepairRequest request, CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return NotFound(ApiResponse<object>.Fail("Tenant was not found."));
        }

        if (request.NormalizeStatusAndActivation)
        {
            if (!ManagedTenantStatuses.Contains(company.Status))
            {
                company.Status = "Active";
            }

            company.IsActive = string.Equals(company.Status, "Active", StringComparison.OrdinalIgnoreCase);
        }

        SubscriptionSnapshot? snapshot = null;
        if (request.RecomputeSubscriptionSnapshot)
        {
            snapshot = await subscriptionEntitlementService.GetCurrentSnapshotAsync(companyId, cancellationToken);
        }

        await AddAuditAsync("PlatformTenantRepairExecuted", companyId, new
        {
            request.NormalizeStatusAndActivation,
            request.RecomputeSubscriptionSnapshot,
            request.Reason,
            synchronizedPlan = snapshot?.PlanName
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new
        {
            companyId,
            company.Status,
            company.IsActive,
            synchronizedPlan = snapshot?.PlanName
        }));
    }

    [HttpGet("diagnostics/read-only")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PlatformReadOnlyDiagnosticDto>>>> GetReadOnlyDiagnostics(CancellationToken cancellationToken)
    {
        var diagnostics = new List<PlatformReadOnlyDiagnosticDto>
        {
            new() { Area = "database", Key = "company_count", Value = (await dbContext.Companies.AsNoTracking().CountAsync(cancellationToken)).ToString() },
            new() { Area = "database", Key = "user_count", Value = (await dbContext.Users.AsNoTracking().CountAsync(cancellationToken)).ToString() },
            new() { Area = "database", Key = "conversation_count", Value = (await dbContext.Conversations.AsNoTracking().CountAsync(cancellationToken)).ToString() },
            new() { Area = "billing", Key = "webhook_event_count", Value = (await dbContext.BillingWebhookEvents.AsNoTracking().CountAsync(cancellationToken)).ToString() },
            new() { Area = "environment", Key = "name", Value = hostEnvironment.EnvironmentName }
        };

        await AddAuditAsync("PlatformDiagnosticsViewed", Guid.Empty, new { count = diagnostics.Count }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyCollection<PlatformReadOnlyDiagnosticDto>>.Ok(diagnostics));
    }

    [HttpGet("system-configuration")]
    [RequiresPermission("platform.admin")]
    public ActionResult<ApiResponse<PlatformSystemConfigurationDto>> GetSystemConfiguration()
    {
        var dto = new PlatformSystemConfigurationDto
        {
            EnvironmentName = hostEnvironment.EnvironmentName,
            AiProvider = aiProviderOptions.Value.Provider,
            OpenAiConfigured = !string.IsNullOrWhiteSpace(openAiOptions.Value.Model) && !string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey),
            AuthRateLimitPerMinute = configuration.GetValue<int?>("ProductionHardening:RateLimits:AuthPerMinute") ?? 10,
            HostApiRateLimitPerMinute = configuration.GetValue<int?>("ProductionHardening:RateLimits:HostApiPerMinute") ?? 120,
            AiGenerationRateLimitPerMinute = configuration.GetValue<int?>("ProductionHardening:RateLimits:AiGenerationPerMinute") ?? 20,
            BillingWebhookEnabled = !string.IsNullOrWhiteSpace(configuration["Billing:Provider"]),
            WhatsAppWebhookEnabled = true
        };

        return Ok(ApiResponse<PlatformSystemConfigurationDto>.Ok(dto));
    }

    [HttpPost("support/impersonation/start")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<PlatformSupportImpersonationStartResponse>>> StartSupportImpersonation(
        [FromBody] PlatformSupportImpersonationStartRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TargetCompanyId == Guid.Empty || request.TargetUserId == Guid.Empty)
        {
            return BadRequest(ApiResponse<PlatformSupportImpersonationStartResponse>.Fail("Target company and user are required."));
        }

        if (string.IsNullOrWhiteSpace(request.ExplicitAuthorizationCode))
        {
            return BadRequest(ApiResponse<PlatformSupportImpersonationStartResponse>.Fail("Explicit authorization code is required for impersonation."));
        }

        var startedAt = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();

        await AddAuditAsync("SupportImpersonationStarted", request.TargetCompanyId, new
        {
            sessionId,
            request.TargetUserId,
            request.Reason,
            authorizationCodeTail = request.ExplicitAuthorizationCode.Length <= 4
                ? request.ExplicitAuthorizationCode
                : request.ExplicitAuthorizationCode[^4..]
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<PlatformSupportImpersonationStartResponse>.Ok(new PlatformSupportImpersonationStartResponse
        {
            SessionId = sessionId,
            TargetCompanyId = request.TargetCompanyId,
            TargetUserId = request.TargetUserId,
            StartedAtUtc = startedAt,
            ExpiresAtUtc = startedAt.AddMinutes(30)
        }));
    }

    [HttpPost("support/impersonation/{sessionId:guid}/end")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<object>>> EndSupportImpersonation(
        Guid sessionId,
        [FromBody] PlatformSupportImpersonationEndRequest request,
        CancellationToken cancellationToken)
    {
        await AddAuditAsync("SupportImpersonationEnded", Guid.Empty, new
        {
            sessionId,
            request.Reason
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new
        {
            sessionId,
            endedAtUtc = DateTimeOffset.UtcNow
        }));
    }

    [HttpGet("incidents")]
    [RequiresPermission("platform.admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PlatformIncidentDto>>>> GetIncidents(CancellationToken cancellationToken)
    {
        var from = DateTimeOffset.UtcNow.AddHours(-24);
        var failedPayments = await dbContext.TenantInvoices
            .AsNoTracking()
            .CountAsync(item => item.FailedAtUtc != null && item.FailedAtUtc >= from, cancellationToken);
        var providerFailures = await dbContext.AuditLogs
            .AsNoTracking()
            .CountAsync(item => item.Action.Contains("Provider")
                && item.Action.Contains("Failed")
                && item.CreatedAt >= from, cancellationToken);

        var incidents = new List<PlatformIncidentDto>();
        if (failedPayments > 0)
        {
            incidents.Add(new PlatformIncidentDto
            {
                IncidentCode = "billing-failed-payments",
                Severity = "high",
                Summary = $"{failedPayments} failed invoice events in the last 24 hours.",
                DetectedAtUtc = DateTimeOffset.UtcNow
            });
        }

        if (providerFailures > 0)
        {
            incidents.Add(new PlatformIncidentDto
            {
                IncidentCode = "provider-failures",
                Severity = "medium",
                Summary = $"{providerFailures} provider-related failures detected in audit events.",
                DetectedAtUtc = DateTimeOffset.UtcNow
            });
        }

        return Ok(ApiResponse<IReadOnlyCollection<PlatformIncidentDto>>.Ok(incidents));
    }

    private async Task<ActionResult<ApiResponse<PlatformTenantDetailDto>>> SetTenantStatusAsync(
        Guid companyId,
        string newStatus,
        bool isActive,
        string auditAction,
        string? reason,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return NotFound(ApiResponse<PlatformTenantDetailDto>.Fail("Tenant was not found."));
        }

        company.Status = newStatus;
        company.IsActive = isActive;

        await AddAuditAsync(auditAction, companyId, new
        {
            reason = NormalizeOptional(reason),
            status = newStatus,
            isActive
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetTenantById(companyId, cancellationToken);
    }

    private async Task AddAuditAsync(string action, Guid entityId, object? metadata, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "PlatformAdmin",
            EntityId = entityId,
            Action = action,
            Details = System.Text.Json.JsonSerializer.Serialize(new
            {
                actorId,
                metadata
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}