using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class SubscriptionEntitlementService(
    ApplicationDbContext dbContext,
    ILogger<SubscriptionEntitlementService> logger) : ISubscriptionEntitlementService
{
    private static readonly string[] ActiveStatuses =
    [
        SubscriptionStatus.Active.ToStorageValue(),
        SubscriptionStatus.Trialing.ToStorageValue(),
        SubscriptionStatus.CancelAtPeriodEnd.ToStorageValue(),
        SubscriptionStatus.PastDue.ToStorageValue()
    ];

    public async Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var subscription = await GetOrCreateCurrentSubscriptionAsync(companyId, cancellationToken);
        return await BuildSnapshotAsync(subscription, cancellationToken);
    }

    public async Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.TenantSubscriptions
            .Include(item => item.SubscriptionPlan)
            .ThenInclude(plan => plan.Entitlements)
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId
                && ActiveStatuses.Contains(item.Status), cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        return await BuildSnapshotAsync(subscription, cancellationToken);
    }

    public async Task EnsureFeatureEnabledAsync(Guid companyId, string featureKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            throw new DomainValidationException("Feature key is required.", "feature_key_required");
        }

        var subscription = await GetOrCreateCurrentSubscriptionAsync(companyId, cancellationToken);
        EnsureStatusAllowsFeatureAccess(subscription, featureKey);
        await dbContext.Entry(subscription)
            .Reference(item => item.SubscriptionPlan)
            .Query()
            .Include(plan => plan.Entitlements)
            .LoadAsync(cancellationToken);

        var entitlement = subscription.SubscriptionPlan.Entitlements
            .FirstOrDefault(item => string.Equals(item.Key, featureKey, StringComparison.Ordinal));

        if (entitlement?.IsEnabled != true)
        {
            throw new ForbiddenOperationException($"Feature '{featureKey}' is not enabled for the current subscription.", "feature_not_enabled");
        }
    }

    public async Task<UsageConsumptionResult> ConsumeQuotaAsync(
        Guid companyId,
        UsageMetric metric,
        long quantity,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            throw new DomainValidationException("Usage quantity must be greater than zero.", "usage_quantity_invalid");
        }

        var normalizedKey = idempotencyKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            throw new DomainValidationException("Idempotency key is required.", "usage_idempotency_key_required");
        }

        var subscription = await GetOrCreateCurrentSubscriptionAsync(companyId, cancellationToken);
        EnsureStatusAllowsQuotaConsumption(subscription, metric);
        await dbContext.Entry(subscription)
            .Reference(item => item.SubscriptionPlan)
            .Query()
            .Include(plan => plan.Entitlements)
            .LoadAsync(cancellationToken);

        var entitlementKey = metric.ToQuotaEntitlementKey();
        var entitlement = subscription.SubscriptionPlan.Entitlements
            .FirstOrDefault(item => string.Equals(item.Key, entitlementKey, StringComparison.Ordinal));

        if (entitlement?.IsEnabled != true)
        {
            throw new ForbiddenOperationException($"Quota metric '{metric}' is not enabled for the current subscription.", "quota_metric_not_enabled");
        }

        var periodStartUtc = subscription.CurrentPeriodStartUtc;
        var periodEndUtc = subscription.CurrentPeriodEndUtc;
        var metricValue = metric.ToStorageValue();

        var priorOperation = await dbContext.UsageOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(operation => operation.CompanyId == companyId
                && operation.Metric == metricValue
                && operation.PeriodStartUtc == periodStartUtc
                && operation.IdempotencyKey == normalizedKey, cancellationToken);
        if (priorOperation is not null)
        {
            var existingRecord = await dbContext.UsageRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.CompanyId == companyId
                    && record.Metric == metricValue
                    && record.PeriodStartUtc == periodStartUtc, cancellationToken);
            var used = existingRecord?.QuantityUsed ?? 0;
            var remaining = entitlement.QuotaLimit is null ? (long?)null : Math.Max(0, entitlement.QuotaLimit.Value - used);
            return new UsageConsumptionResult(metric, entitlement.QuotaLimit, Math.Max(0, used - priorOperation.Quantity), used, entitlement.IsUnlimited || entitlement.QuotaLimit is null, true);
        }

        var usageRecord = await dbContext.UsageRecords
            .FirstOrDefaultAsync(record => record.CompanyId == companyId
                && record.Metric == metricValue
                && record.PeriodStartUtc == periodStartUtc, cancellationToken);

        usageRecord ??= new UsageRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Metric = metricValue,
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            QuantityUsed = 0
        };

        var previousUsage = usageRecord.QuantityUsed;
        var updatedUsage = checked(previousUsage + quantity);
        var limit = entitlement.QuotaLimit;
        var unlimited = entitlement.IsUnlimited || limit is null;

        if (!unlimited && limit.HasValue && updatedUsage > limit.Value)
        {
            throw new QuotaExceededException(metricValue, limit, quantity, previousUsage);
        }

        usageRecord.QuantityUsed = updatedUsage;
        usageRecord.PeriodEndUtc = periodEndUtc;

        if (usageRecord.Id == Guid.Empty)
        {
            usageRecord.Id = Guid.NewGuid();
        }

        if (dbContext.Entry(usageRecord).State == EntityState.Detached)
        {
            await dbContext.UsageRecords.AddAsync(usageRecord, cancellationToken);
        }

        await dbContext.UsageOperations.AddAsync(new UsageOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Metric = metricValue,
            IdempotencyKey = normalizedKey,
            PeriodStartUtc = periodStartUtc,
            Quantity = quantity
        }, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            logger.LogInformation(
                exception,
                "Usage operation idempotency replay detected. CompanyId={CompanyId} Metric={Metric} PeriodStartUtc={PeriodStartUtc} IdempotencyKey={IdempotencyKey}",
                companyId,
                metricValue,
                periodStartUtc,
                normalizedKey);

            var existingRecord = await dbContext.UsageRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.CompanyId == companyId
                    && record.Metric == metricValue
                    && record.PeriodStartUtc == periodStartUtc, cancellationToken);
            return new UsageConsumptionResult(metric, limit, previousUsage, existingRecord?.QuantityUsed ?? previousUsage, unlimited, true);
        }

        return new UsageConsumptionResult(metric, limit, previousUsage, updatedUsage, unlimited, false);
    }

    public async Task<SubscriptionSnapshot> UpdatePlanAsync(
        Guid companyId,
        Guid? planId,
        string? planName,
        string? notes,
        CancellationToken cancellationToken)
    {
        var targetPlan = await ResolvePlanAsync(planId, planName, cancellationToken);
        var current = await GetOrCreateCurrentSubscriptionAsync(companyId, cancellationToken);

        current.SubscriptionPlanId = targetPlan.Id;
        current.Status = SubscriptionStatus.Active.ToStorageValue();
        current.CancelAtPeriodEnd = false;
        current.EndedAtUtc = null;
        current.Notes = string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim();

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantSubscription),
            EntityId = current.Id,
            Action = "PlanUpdated",
            Details = $"{{\"companyId\":\"{companyId}\",\"subscriptionId\":\"{current.Id}\",\"planId\":\"{targetPlan.Id}\",\"planName\":\"{targetPlan.Name}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(current).Reference(item => item.SubscriptionPlan).LoadAsync(cancellationToken);
        await dbContext.Entry(current.SubscriptionPlan).Collection(item => item.Entitlements).LoadAsync(cancellationToken);
        return await BuildSnapshotAsync(current, cancellationToken);
    }

    private async Task<SubscriptionSnapshot> BuildSnapshotAsync(TenantSubscription subscription, CancellationToken cancellationToken)
    {
        await dbContext.Entry(subscription)
            .Reference(item => item.SubscriptionPlan)
            .Query()
            .Include(plan => plan.Entitlements)
            .LoadAsync(cancellationToken);

        var features = subscription.SubscriptionPlan.Entitlements
            .Where(item => !item.Key.StartsWith("Quota.", StringComparison.Ordinal))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new FeatureSnapshot(item.Key, item.IsEnabled))
            .ToList();

        var metricByEntitlement = Enum.GetValues<UsageMetric>()
            .ToDictionary(metric => metric.ToQuotaEntitlementKey(), metric => metric, StringComparer.Ordinal);

        var metricEntitlements = subscription.SubscriptionPlan.Entitlements
            .Where(item => item.Key.StartsWith("Quota.", StringComparison.Ordinal) && metricByEntitlement.ContainsKey(item.Key))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToList();

        var usageByMetric = await dbContext.UsageRecords
            .AsNoTracking()
            .Where(record => record.CompanyId == subscription.CompanyId
                && record.PeriodStartUtc == subscription.CurrentPeriodStartUtc
                && record.PeriodEndUtc == subscription.CurrentPeriodEndUtc)
            .ToDictionaryAsync(record => record.Metric, record => record.QuantityUsed, StringComparer.Ordinal, cancellationToken);

        var quotas = metricEntitlements
            .Select(entitlement =>
            {
                var metric = metricByEntitlement[entitlement.Key];
                var used = usageByMetric.GetValueOrDefault(metric.ToStorageValue(), 0);
                var unlimited = entitlement.IsUnlimited || entitlement.QuotaLimit is null;
                var remaining = unlimited
                    ? (long?)null
                    : Math.Max(0, (entitlement.QuotaLimit ?? 0) - used);

                return new QuotaSnapshot(
                    metric,
                    entitlement.Key,
                    entitlement.QuotaLimit,
                    used,
                    remaining,
                    unlimited,
                    entitlement.Unit ?? "count",
                    subscription.CurrentPeriodStartUtc,
                    subscription.CurrentPeriodEndUtc);
            })
            .ToList();

        return new SubscriptionSnapshot(
            subscription.CompanyId,
            subscription.Id,
            subscription.SubscriptionPlanId,
            subscription.SubscriptionPlan.Name,
            subscription.SubscriptionPlan.DisplayName,
            subscription.Status,
            subscription.SubscriptionPlan.IsEnterprise,
            subscription.CurrentPeriodStartUtc,
            subscription.CurrentPeriodEndUtc,
            features,
            quotas);
    }

    private async Task<TenantSubscription> GetOrCreateCurrentSubscriptionAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await dbContext.TenantSubscriptions
            .Include(item => item.SubscriptionPlan)
            .ThenInclude(plan => plan.Entitlements)
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId
                && ActiveStatuses.Contains(item.Status), cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var defaultPlan = await ResolveDefaultProvisioningPlanAsync(cancellationToken);

        var periodStartUtc = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEndUtc = periodStartUtc.AddMonths(1).AddTicks(-1);

        var subscription = new TenantSubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SubscriptionPlanId = defaultPlan.Id,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = periodStartUtc,
            CurrentPeriodEndUtc = periodEndUtc
        };

        logger.LogInformation(
            "Provisioning default subscription for company {CompanyId} using plan {PlanId} ({PlanName}) because no active subscription exists.",
            companyId,
            defaultPlan.Id,
            defaultPlan.Name);

        await dbContext.TenantSubscriptions.AddAsync(subscription, cancellationToken);
        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantSubscription),
            EntityId = subscription.Id,
            Action = "PlanProvisioned",
            Details = $"{{\"companyId\":\"{companyId}\",\"planId\":\"{defaultPlan.Id}\",\"planName\":\"{defaultPlan.Name}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(subscription).Reference(item => item.SubscriptionPlan).LoadAsync(cancellationToken);
        await dbContext.Entry(subscription.SubscriptionPlan).Collection(item => item.Entitlements).LoadAsync(cancellationToken);
        return subscription;
    }

    private async Task<SubscriptionPlan> ResolveDefaultProvisioningPlanAsync(CancellationToken cancellationToken)
    {
        var defaultPlan = await dbContext.SubscriptionPlans
            .OrderBy(plan => plan.SortOrder)
            .FirstOrDefaultAsync(plan => plan.IsActive
                && string.Equals(plan.Name, "Free", StringComparison.OrdinalIgnoreCase), cancellationToken);

        defaultPlan ??= await dbContext.SubscriptionPlans
            .OrderBy(plan => plan.SortOrder)
            .FirstOrDefaultAsync(plan => plan.IsActive
                && string.Equals(plan.Name, "Professional", StringComparison.OrdinalIgnoreCase), cancellationToken);

        defaultPlan ??= await dbContext.SubscriptionPlans
            .OrderBy(plan => plan.SortOrder)
            .FirstOrDefaultAsync(plan => plan.IsActive, cancellationToken);

        defaultPlan ??= await dbContext.SubscriptionPlans
            .OrderBy(plan => plan.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (defaultPlan is not null)
        {
            return defaultPlan;
        }

        logger.LogError("Unable to provision a default subscription because no subscription plans are configured.");
        throw new InvalidOperationException("No subscription plans are configured.");
    }

    private async Task<SubscriptionPlan> ResolvePlanAsync(Guid? planId, string? planName, CancellationToken cancellationToken)
    {
        if (planId is null && string.IsNullOrWhiteSpace(planName))
        {
            throw new DomainValidationException("Either planId or planName must be provided.", "plan_target_required");
        }

        var query = dbContext.SubscriptionPlans
            .Where(item => item.IsActive);

        SubscriptionPlan? plan = null;
        if (planId is { } requestedId && requestedId != Guid.Empty)
        {
            plan = await query.FirstOrDefaultAsync(item => item.Id == requestedId, cancellationToken);
        }

        if (plan is null && !string.IsNullOrWhiteSpace(planName))
        {
            var normalized = planName.Trim();
            plan = await query.FirstOrDefaultAsync(item =>
                string.Equals(item.Name, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.DisplayName, normalized, StringComparison.OrdinalIgnoreCase), cancellationToken);
        }

        if (plan is null)
        {
            throw new ResourceNotFoundException("Subscription plan was not found.", "plan_not_found");
        }

        return plan;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void EnsureStatusAllowsFeatureAccess(TenantSubscription subscription, string featureKey)
    {
        if (string.Equals(subscription.Status, SubscriptionStatus.Suspended.ToStorageValue(), StringComparison.Ordinal)
            || string.Equals(subscription.Status, SubscriptionStatus.Cancelled.ToStorageValue(), StringComparison.Ordinal))
        {
            throw new ForbiddenOperationException(
                $"Subscription status '{subscription.Status}' does not allow feature access.",
                "subscription_inactive");
        }

        if (string.Equals(subscription.Status, SubscriptionStatus.PastDue.ToStorageValue(), StringComparison.Ordinal)
            && (string.Equals(featureKey, FeatureKeys.AiConcierge, StringComparison.Ordinal)
                || string.Equals(featureKey, FeatureKeys.WhatsApp, StringComparison.Ordinal)
                || string.Equals(featureKey, FeatureKeys.AdvancedIntegrations, StringComparison.Ordinal)))
        {
            throw new ForbiddenOperationException(
                $"Feature '{featureKey}' is temporarily unavailable while subscription is past due.",
                "subscription_past_due_limited");
        }
    }

    private static void EnsureStatusAllowsQuotaConsumption(TenantSubscription subscription, UsageMetric metric)
    {
        if (string.Equals(subscription.Status, SubscriptionStatus.Suspended.ToStorageValue(), StringComparison.Ordinal)
            || string.Equals(subscription.Status, SubscriptionStatus.Cancelled.ToStorageValue(), StringComparison.Ordinal))
        {
            throw new ForbiddenOperationException(
                $"Subscription status '{subscription.Status}' does not allow usage consumption.",
                "subscription_inactive");
        }

        if (string.Equals(subscription.Status, SubscriptionStatus.PastDue.ToStorageValue(), StringComparison.Ordinal)
            && (metric == UsageMetric.AiRequests || metric == UsageMetric.WhatsAppMessages || metric == UsageMetric.ApiRequests))
        {
            throw new ForbiddenOperationException(
                $"Usage metric '{metric}' is temporarily unavailable while subscription is past due.",
                "subscription_past_due_limited");
        }
    }
}