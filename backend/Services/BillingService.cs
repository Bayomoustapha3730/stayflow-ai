using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Billing;
using StayFlow.Api.Models;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Services;

public sealed class BillingService(
    ApplicationDbContext dbContext,
    ICurrentTenantContext tenantContext,
    ISubscriptionEntitlementService subscriptionEntitlementService,
    IBillingProvider billingProvider,
    IOptions<BillingOptions> billingOptions,
    ILogger<BillingService> logger) : IBillingService
{
    public async Task<ApiResponse<CreateCheckoutSessionResponse>> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<CreateCheckoutSessionResponse>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<CreateCheckoutSessionResponse>.Fail(authorization.Error);
        }

        var planName = request.PlanName.Trim();
        if (string.IsNullOrWhiteSpace(planName))
        {
            return ApiResponse<CreateCheckoutSessionResponse>.Fail("Plan name is required.");
        }

        var options = billingOptions.Value;
        if (!options.PlanPriceIds.TryGetValue(planName, out var priceId) || string.IsNullOrWhiteSpace(priceId))
        {
            return ApiResponse<CreateCheckoutSessionResponse>.Fail("Plan price mapping is not configured.");
        }

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return ApiResponse<CreateCheckoutSessionResponse>.Fail("Organization was not found.");
        }

        var customerId = company.StripeCustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            customerId = await billingProvider.EnsureCustomerAsync(new BillingCustomerRequest(company.Id, company.Name, company.Email), cancellationToken);
            company.StripeCustomerId = customerId;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var checkoutUrl = await billingProvider.CreateCheckoutSessionAsync(new CheckoutSessionRequest(
            companyId,
            customerId,
            priceId,
            options.CheckoutSuccessUrl,
            options.CheckoutCancelUrl,
            tenantContext.CorrelationId,
            request.TrialDays), cancellationToken);

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantSubscription),
            EntityId = companyId,
            Action = "BillingCheckoutCreated",
            Details = $"{{\"companyId\":\"{companyId}\",\"plan\":\"{planName}\",\"paymentMethod\":\"{(request.PaymentMethod ?? string.Empty).Trim()}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<CreateCheckoutSessionResponse>.Ok(new CreateCheckoutSessionResponse
        {
            CheckoutUrl = checkoutUrl,
            Provider = billingProvider.ProviderName
        });
    }

    public async Task<ApiResponse<CreateBillingPortalSessionResponse>> CreateBillingPortalSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<CreateBillingPortalSessionResponse>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<CreateBillingPortalSessionResponse>.Fail(authorization.Error);
        }

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return ApiResponse<CreateBillingPortalSessionResponse>.Fail("Organization was not found.");
        }

        var customerId = company.StripeCustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return ApiResponse<CreateBillingPortalSessionResponse>.Fail("Billing customer is not configured for this tenant.");
        }

        var portalUrl = await billingProvider.CreateBillingPortalSessionAsync(new BillingPortalRequest(
            companyId,
            customerId,
            billingOptions.Value.BillingPortalReturnUrl,
            tenantContext.CorrelationId), cancellationToken);

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantSubscription),
            EntityId = companyId,
            Action = "BillingPortalCreated",
            Details = $"{{\"companyId\":\"{companyId}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<CreateBillingPortalSessionResponse>.Ok(new CreateBillingPortalSessionResponse
        {
            PortalUrl = portalUrl,
            Provider = billingProvider.ProviderName
        });
    }

    public async Task<ApiResponse<CreateBillingPortalSessionResponse>> CreatePaymentMethodManagementSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<CreateBillingPortalSessionResponse>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<CreateBillingPortalSessionResponse>.Fail(authorization.Error);
        }

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return ApiResponse<CreateBillingPortalSessionResponse>.Fail("Organization was not found.");
        }

        var customerId = company.StripeCustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return ApiResponse<CreateBillingPortalSessionResponse>.Fail("Billing customer is not configured for this tenant.");
        }

        var portalUrl = await billingProvider.CreatePaymentMethodPortalSessionAsync(new BillingPortalRequest(
            companyId,
            customerId,
            billingOptions.Value.BillingPortalReturnUrl,
            tenantContext.CorrelationId), cancellationToken);

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantSubscription),
            EntityId = companyId,
            Action = "BillingPaymentMethodPortalCreated",
            Details = $"{{\"companyId\":\"{companyId}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<CreateBillingPortalSessionResponse>.Ok(new CreateBillingPortalSessionResponse
        {
            PortalUrl = portalUrl,
            Provider = billingProvider.ProviderName
        });
    }

    public async Task<ApiResponse<BillingSubscriptionResponse?>> GetSubscriptionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<BillingSubscriptionResponse?>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<BillingSubscriptionResponse?>.Fail(authorization.Error);
        }

        var subscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .Include(item => item.SubscriptionPlan)
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        if (subscription is null)
        {
            return ApiResponse<BillingSubscriptionResponse?>.Ok(null, "No active subscription.");
        }

        return ApiResponse<BillingSubscriptionResponse?>.Ok(MapSubscriptionResponse(companyId, subscription));
    }

    public async Task<ApiResponse<IReadOnlyCollection<BillingPlanResponse>>> GetPlansAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<IReadOnlyCollection<BillingPlanResponse>>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<IReadOnlyCollection<BillingPlanResponse>>.Fail(authorization.Error);
        }

        var company = await dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return ApiResponse<IReadOnlyCollection<BillingPlanResponse>>.Fail("Organization was not found.");
        }

        var currentSubscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);

        var activePlans = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .Include(plan => plan.Entitlements)
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.SortOrder)
            .ThenBy(plan => plan.DisplayName)
            .ToListAsync(cancellationToken);

        var options = billingOptions.Value;
        var currency = ResolveCurrency(company.CountryCode, options);
        var plans = activePlans
            .Select(plan => MapPlanResponse(plan, currentSubscription?.SubscriptionPlanId == plan.Id, currency, options))
            .ToList();

        return ApiResponse<IReadOnlyCollection<BillingPlanResponse>>.Ok(plans);
    }

    public async Task<ApiResponse<IReadOnlyCollection<BillingPaymentOptionResponse>>> GetPaymentOptionsAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<IReadOnlyCollection<BillingPaymentOptionResponse>>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<IReadOnlyCollection<BillingPaymentOptionResponse>>.Fail(authorization.Error);
        }

        var company = await dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return ApiResponse<IReadOnlyCollection<BillingPaymentOptionResponse>>.Fail("Organization was not found.");
        }

        var methods = ResolvePaymentMethods(company.CountryCode, billingProvider.ProviderName, billingOptions.Value)
            .Select(MapPaymentOption)
            .ToList();

        return ApiResponse<IReadOnlyCollection<BillingPaymentOptionResponse>>.Ok(methods);
    }

    public async Task<ApiResponse<BillingSubscriptionResponse>> ChangeSubscriptionPlanAsync(ChangeSubscriptionPlanRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail(authorization.Error);
        }

        var planName = request.PlanName.Trim();
        if (string.IsNullOrWhiteSpace(planName))
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail("Plan name is required.");
        }

        var options = billingOptions.Value;
        if (!options.PlanPriceIds.TryGetValue(planName, out var priceId) || string.IsNullOrWhiteSpace(priceId))
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail("Plan price mapping is not configured.");
        }

        var subscription = await dbContext.TenantSubscriptions
            .Include(item => item.SubscriptionPlan)
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        if (subscription is null)
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail("Subscription was not found.");
        }

        if (string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId))
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail("Provider subscription ID is not configured for this tenant.");
        }

        var snapshot = await billingProvider.ChangeSubscriptionPlanAsync(new ChangeSubscriptionPlanProviderRequest(
            subscription.ExternalSubscriptionId,
            priceId,
            tenantContext.CorrelationId), cancellationToken);

        await ApplyProviderSnapshotAsync(subscription, snapshot, cancellationToken);

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantSubscription),
            EntityId = subscription.Id,
            Action = "BillingPlanChanged",
            Details = $"{{\"companyId\":\"{companyId}\",\"plan\":\"{planName}\",\"priceId\":\"{priceId}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<BillingSubscriptionResponse>.Ok(MapSubscriptionResponse(companyId, subscription), "Plan changed successfully.");
    }

    public async Task<ApiResponse<BillingSubscriptionResponse>> CancelSubscriptionAsync(CancelSubscriptionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail(authorization.Error);
        }

        var subscription = await dbContext.TenantSubscriptions
            .Include(item => item.SubscriptionPlan)
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        if (subscription is null)
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail("Subscription was not found.");
        }

        if (string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId))
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail("Provider subscription ID is not configured for this tenant.");
        }

        var snapshot = await billingProvider.CancelSubscriptionAsync(new CancelSubscriptionProviderRequest(
            subscription.ExternalSubscriptionId,
            request.AtPeriodEnd,
            tenantContext.CorrelationId), cancellationToken);

        await ApplyProviderSnapshotAsync(subscription, snapshot, cancellationToken);

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantSubscription),
            EntityId = subscription.Id,
            Action = request.AtPeriodEnd ? "BillingCancelScheduled" : "BillingCancelledImmediately",
            Details = $"{{\"companyId\":\"{companyId}\",\"atPeriodEnd\":{request.AtPeriodEnd.ToString().ToLowerInvariant()}}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<BillingSubscriptionResponse>.Ok(MapSubscriptionResponse(companyId, subscription), "Subscription cancellation updated.");
    }

    public async Task<ApiResponse<BillingSubscriptionResponse>> ResumeSubscriptionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail(authorization.Error);
        }

        var subscription = await dbContext.TenantSubscriptions
            .Include(item => item.SubscriptionPlan)
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        if (subscription is null)
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail("Subscription was not found.");
        }

        if (string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId))
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail("Provider subscription ID is not configured for this tenant.");
        }

        var snapshot = await billingProvider.ResumeSubscriptionAsync(new ResumeSubscriptionProviderRequest(
            subscription.ExternalSubscriptionId,
            tenantContext.CorrelationId), cancellationToken);

        await ApplyProviderSnapshotAsync(subscription, snapshot, cancellationToken);

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantSubscription),
            EntityId = subscription.Id,
            Action = "BillingSubscriptionResumed",
            Details = $"{{\"companyId\":\"{companyId}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<BillingSubscriptionResponse>.Ok(MapSubscriptionResponse(companyId, subscription), "Subscription resumed.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<TenantInvoiceDto>>> GetInvoicesAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<IReadOnlyCollection<TenantInvoiceDto>>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<IReadOnlyCollection<TenantInvoiceDto>>.Fail(authorization.Error);
        }

        var invoices = await dbContext.TenantInvoices
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new TenantInvoiceDto
            {
                Id = item.Id,
                ExternalInvoiceId = item.ExternalInvoiceId,
                Status = item.Status,
                AmountDue = item.AmountDue,
                AmountPaid = item.AmountPaid,
                Currency = item.Currency,
                PeriodStartUtc = item.PeriodStartUtc,
                PeriodEndUtc = item.PeriodEndUtc,
                PaidAtUtc = item.PaidAtUtc,
                FailedAtUtc = item.FailedAtUtc,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<IReadOnlyCollection<TenantInvoiceDto>>.Ok(invoices);
    }

    public async Task<ApiResponse<UsageSummaryResponse>> GetUsageSummaryAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<UsageSummaryResponse>.Fail(error);
        }

        var authorization = await EnsureOwnerOrAdministratorAsync(cancellationToken);
        if (!authorization.Success)
        {
            return ApiResponse<UsageSummaryResponse>.Fail(authorization.Error);
        }

        var snapshot = await subscriptionEntitlementService.TryGetCurrentSnapshotAsync(companyId, cancellationToken);
        if (snapshot is null)
        {
            return ApiResponse<UsageSummaryResponse>.Ok(new UsageSummaryResponse
            {
                CompanyId = companyId,
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                Metrics = []
            });
        }

        var metrics = snapshot.Quotas
            .OrderBy(item => item.Metric)
            .Select(item => new UsageMetricSummaryDto
            {
                Metric = item.Metric.ToStorageValue(),
                EntitlementKey = item.EntitlementKey,
                Used = item.Used,
                Limit = item.Limit,
                Remaining = item.Remaining,
                IsUnlimited = item.IsUnlimited,
                Unit = item.Unit,
                PeriodStartUtc = item.PeriodStartUtc,
                PeriodEndUtc = item.PeriodEndUtc
            })
            .ToList();

        return ApiResponse<UsageSummaryResponse>.Ok(new UsageSummaryResponse
        {
            CompanyId = companyId,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Metrics = metrics
        });
    }

    public async Task<BillingWebhookProcessingResult> ProcessStripeWebhookAsync(string rawBody, string signatureHeader, CancellationToken cancellationToken)
    {
        var envelope = billingProvider.ValidateAndParseWebhook(rawBody, signatureHeader);

        var existing = await dbContext.BillingWebhookEvents
            .FirstOrDefaultAsync(item => item.Provider == billingProvider.ProviderName && item.EventId == envelope.EventId, cancellationToken);
        if (existing is not null)
        {
            existing.WasDuplicate = true;
            existing.ProcessedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new BillingWebhookProcessingResult
            {
                EventId = envelope.EventId,
                EventType = envelope.EventType,
                WasDuplicate = true,
                AppliedStateChange = false
            };
        }

        var applied = false;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.BillingWebhookEvents.AddAsync(new BillingWebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = billingProvider.ProviderName,
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            CustomerId = envelope.CustomerId,
            SubscriptionId = envelope.SubscriptionId,
            EventCreatedAtUtc = envelope.EventCreatedAtUtc,
            PayloadHash = envelope.PayloadHash,
            ProcessedAtUtc = DateTimeOffset.UtcNow,
            WasDuplicate = false
        }, cancellationToken);

        try
        {
            switch (envelope.EventType)
            {
                case "checkout.session.completed":
                    applied = await ApplyCheckoutSessionCompletedAsync(envelope, cancellationToken);
                    break;
                case "customer.subscription.created":
                case "customer.subscription.updated":
                case "customer.subscription.deleted":
                    applied = await ApplySubscriptionEventAsync(envelope, cancellationToken);
                    break;
                case "invoice.paid":
                case "invoice.payment_failed":
                    applied = await ApplyInvoiceEventAsync(envelope, cancellationToken);
                    break;
                default:
                    logger.LogInformation("Ignored unsupported Stripe event type {EventType}", envelope.EventType);
                    break;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueWebhookEventViolation(exception))
        {
            logger.LogInformation("Duplicate Stripe webhook event detected via unique constraint for {EventId}", envelope.EventId);
            await transaction.RollbackAsync(cancellationToken);
            return new BillingWebhookProcessingResult
            {
                EventId = envelope.EventId,
                EventType = envelope.EventType,
                WasDuplicate = true,
                AppliedStateChange = false
            };
        }

        return new BillingWebhookProcessingResult
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            WasDuplicate = false,
            AppliedStateChange = applied
        };
    }

    private async Task<bool> ApplyCheckoutSessionCompletedAsync(BillingWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        var data = envelope.DataObject;
        if (!data.TryGetProperty("metadata", out var metadata) || !metadata.TryGetProperty("company_id", out var companyElement))
        {
            return false;
        }

        if (!Guid.TryParse(companyElement.GetString(), out var companyId) || companyId == Guid.Empty)
        {
            return false;
        }

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return false;
        }

        var customerId = data.TryGetProperty("customer", out var customerElement) ? customerElement.GetString() : null;
        var subscriptionId = data.TryGetProperty("subscription", out var subscriptionElement) ? subscriptionElement.GetString() : null;
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            company.StripeCustomerId = customerId;
        }

        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            var subscription = await dbContext.TenantSubscriptions
                .OrderByDescending(item => item.CurrentPeriodStartUtc)
                .FirstOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
            if (subscription is not null)
            {
                subscription.ExternalSubscriptionId = subscriptionId;
            }
        }

        return true;
    }

    private async Task<bool> ApplySubscriptionEventAsync(BillingWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        var company = await ResolveCompanyForWebhookAsync(envelope, cancellationToken);
        if (company is null)
        {
            return false;
        }

        var subscription = await dbContext.TenantSubscriptions
            .Include(item => item.SubscriptionPlan)
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .FirstOrDefaultAsync(item => item.CompanyId == company.Id, cancellationToken);
        if (subscription is null)
        {
            return false;
        }

        if (subscription.LastProviderEventCreatedAtUtc is { } lastProcessed && lastProcessed > envelope.EventCreatedAtUtc)
        {
            return false;
        }

        subscription.LastProviderEventCreatedAtUtc = envelope.EventCreatedAtUtc;
        subscription.ExternalSubscriptionId = envelope.SubscriptionId;

        var data = envelope.DataObject;
        if (data.TryGetProperty("current_period_start", out var startElement) && startElement.ValueKind == JsonValueKind.Number)
        {
            subscription.CurrentPeriodStartUtc = DateTimeOffset.FromUnixTimeSeconds(startElement.GetInt64());
        }

        if (data.TryGetProperty("current_period_end", out var endElement) && endElement.ValueKind == JsonValueKind.Number)
        {
            subscription.CurrentPeriodEndUtc = DateTimeOffset.FromUnixTimeSeconds(endElement.GetInt64());
        }

        if (data.TryGetProperty("cancel_at_period_end", out var cancelElement) && cancelElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            subscription.CancelAtPeriodEnd = cancelElement.GetBoolean();
        }

        if (data.TryGetProperty("trial_end", out var trialEndElement) && trialEndElement.ValueKind == JsonValueKind.Number)
        {
            subscription.TrialEndsAtUtc = DateTimeOffset.FromUnixTimeSeconds(trialEndElement.GetInt64());
        }

        if (data.TryGetProperty("items", out var itemsElement)
            && itemsElement.TryGetProperty("data", out var itemData)
            && itemData.ValueKind == JsonValueKind.Array
            && itemData.GetArrayLength() > 0)
        {
            var first = itemData[0];
            if (first.TryGetProperty("price", out var priceElement)
                && priceElement.TryGetProperty("id", out var priceIdElement))
            {
                subscription.ExternalPriceId = priceIdElement.GetString();
            }
        }

        // Snapshot refresh tolerates partial payloads and ensures we stay aligned with provider state.
        if (!string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId))
        {
            try
            {
                var snapshot = await billingProvider.GetSubscriptionSnapshotAsync(subscription.ExternalSubscriptionId, cancellationToken);
                await ApplyProviderSnapshotAsync(subscription, snapshot, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to synchronize Stripe subscription snapshot for {SubscriptionId}", subscription.ExternalSubscriptionId);
            }
        }

        subscription.Status = MapSubscriptionStatus(envelope.EventType, data);
        if (subscription.Status == SubscriptionStatus.Cancelled.ToStorageValue())
        {
            subscription.EndedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantSubscription),
            EntityId = subscription.Id,
            Action = "BillingWebhookSubscriptionUpdated",
            Details = $"{{\"companyId\":\"{company.Id}\",\"status\":\"{subscription.Status}\",\"eventType\":\"{envelope.EventType}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        return true;
    }

    private async Task<Company?> ResolveCompanyForWebhookAsync(BillingWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(envelope.CustomerId))
        {
            var companyByCustomer = await dbContext.Companies
                .FirstOrDefaultAsync(item => item.StripeCustomerId == envelope.CustomerId, cancellationToken);
            if (companyByCustomer is not null)
            {
                return companyByCustomer;
            }
        }

        if (!string.IsNullOrWhiteSpace(envelope.SubscriptionId))
        {
            var companyBySubscription = await dbContext.TenantSubscriptions
                .Where(item => item.ExternalSubscriptionId == envelope.SubscriptionId)
                .Select(item => item.Company)
                .FirstOrDefaultAsync(cancellationToken);
            return companyBySubscription;
        }

        return null;
    }

    private async Task<bool> ApplyInvoiceEventAsync(BillingWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        var company = !string.IsNullOrWhiteSpace(envelope.CustomerId)
            ? await dbContext.Companies.FirstOrDefaultAsync(item => item.StripeCustomerId == envelope.CustomerId, cancellationToken)
            : null;
        if (company is null)
        {
            return false;
        }

        var data = envelope.DataObject;
        if (!data.TryGetProperty("id", out var invoiceIdElement))
        {
            return false;
        }

        var invoiceId = invoiceIdElement.GetString();
        if (string.IsNullOrWhiteSpace(invoiceId))
        {
            return false;
        }

        var invoice = await dbContext.TenantInvoices
            .FirstOrDefaultAsync(item => item.ExternalInvoiceId == invoiceId, cancellationToken)
            ?? new TenantInvoice
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                ExternalInvoiceId = invoiceId
            };

        invoice.ExternalCustomerId = envelope.CustomerId;
        invoice.ExternalSubscriptionId = envelope.SubscriptionId;
        invoice.Status = data.TryGetProperty("status", out var statusElement) ? statusElement.GetString() ?? "Open" : "Open";
        invoice.AmountDue = data.TryGetProperty("amount_due", out var dueElement) && dueElement.ValueKind == JsonValueKind.Number ? dueElement.GetInt64() : 0;
        invoice.AmountPaid = data.TryGetProperty("amount_paid", out var paidElement) && paidElement.ValueKind == JsonValueKind.Number ? paidElement.GetInt64() : 0;
        invoice.Currency = data.TryGetProperty("currency", out var currencyElement) ? currencyElement.GetString() ?? "usd" : "usd";

        if (data.TryGetProperty("period_start", out var periodStartElement) && periodStartElement.ValueKind == JsonValueKind.Number)
        {
            invoice.PeriodStartUtc = DateTimeOffset.FromUnixTimeSeconds(periodStartElement.GetInt64());
        }

        if (data.TryGetProperty("period_end", out var periodEndElement) && periodEndElement.ValueKind == JsonValueKind.Number)
        {
            invoice.PeriodEndUtc = DateTimeOffset.FromUnixTimeSeconds(periodEndElement.GetInt64());
        }

        if (envelope.EventType == "invoice.paid")
        {
            invoice.PaidAtUtc = DateTimeOffset.UtcNow;
            invoice.FailedAtUtc = null;
        }

        if (envelope.EventType == "invoice.payment_failed")
        {
            invoice.FailedAtUtc = DateTimeOffset.UtcNow;

            var subscription = await dbContext.TenantSubscriptions
                .OrderByDescending(item => item.CurrentPeriodStartUtc)
                .FirstOrDefaultAsync(item => item.CompanyId == company.Id, cancellationToken);
            if (subscription is not null)
            {
                subscription.Status = SubscriptionStatus.PastDue.ToStorageValue();
            }
        }

        if (dbContext.Entry(invoice).State == EntityState.Detached)
        {
            await dbContext.TenantInvoices.AddAsync(invoice, cancellationToken);
        }

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantInvoice),
            EntityId = invoice.Id,
            Action = envelope.EventType == "invoice.paid" ? "InvoicePaid" : "InvoicePaymentFailed",
            Details = $"{{\"companyId\":\"{company.Id}\",\"invoiceId\":\"{invoice.ExternalInvoiceId}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        return true;
    }

    private static string MapSubscriptionStatus(string eventType, JsonElement data)
    {
        if (eventType == "customer.subscription.deleted")
        {
            return SubscriptionStatus.Cancelled.ToStorageValue();
        }

        var stripeStatus = data.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? string.Empty
            : string.Empty;

        if (string.Equals(stripeStatus, "trialing", StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionStatus.Trialing.ToStorageValue();
        }

        if (string.Equals(stripeStatus, "active", StringComparison.OrdinalIgnoreCase))
        {
            var cancelAtPeriodEnd = data.TryGetProperty("cancel_at_period_end", out var cancelElement)
                && cancelElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                && cancelElement.GetBoolean();
            return cancelAtPeriodEnd
                ? SubscriptionStatus.CancelAtPeriodEnd.ToStorageValue()
                : SubscriptionStatus.Active.ToStorageValue();
        }

        if (string.Equals(stripeStatus, "past_due", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stripeStatus, "unpaid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stripeStatus, "incomplete", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stripeStatus, "incomplete_expired", StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionStatus.PastDue.ToStorageValue();
        }

        if (string.Equals(stripeStatus, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionStatus.Cancelled.ToStorageValue();
        }

        return SubscriptionStatus.Active.ToStorageValue();
    }

    private static bool IsUniqueWebhookEventViolation(DbUpdateException exception)
    {
        return exception.InnerException?.Message.Contains("IX_BillingWebhookEvents_Provider_EventId", StringComparison.OrdinalIgnoreCase) == true
            || exception.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static BillingSubscriptionResponse MapSubscriptionResponse(Guid companyId, TenantSubscription subscription)
    {
        return new BillingSubscriptionResponse
        {
            CompanyId = companyId,
            Status = subscription.Status,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            CurrentPeriodStartUtc = subscription.CurrentPeriodStartUtc,
            CurrentPeriodEndUtc = subscription.CurrentPeriodEndUtc,
            TrialEndsAtUtc = subscription.TrialEndsAtUtc,
            PlanName = subscription.SubscriptionPlan?.DisplayName ?? subscription.SubscriptionPlan?.Name,
            ExternalSubscriptionId = subscription.ExternalSubscriptionId,
            ExternalPriceId = subscription.ExternalPriceId
        };
    }

    private static BillingPlanResponse MapPlanResponse(
        SubscriptionPlan plan,
        bool isCurrentPlan,
        string currency,
        BillingOptions options)
    {
        var propertyLimit = ReadQuotaLimit(plan, UsageMetric.Properties.ToQuotaEntitlementKey());
        var teamLimit = ReadQuotaLimit(plan, UsageMetric.Users.ToQuotaEntitlementKey());
        var aiRequestLimit = ReadQuotaLimit(plan, UsageMetric.AiRequests.ToQuotaEntitlementKey());
        var whatsAppLimit = ReadQuotaLimit(plan, UsageMetric.WhatsAppMessages.ToQuotaEntitlementKey());

        options.PlanMonthlyAmountsMinor.TryGetValue(plan.Name, out var amountByName);
        options.PlanMonthlyAmountsMinor.TryGetValue(plan.DisplayName, out var amountByDisplay);

        options.PlanTrialDays.TryGetValue(plan.Name, out var trialByName);
        options.PlanTrialDays.TryGetValue(plan.DisplayName, out var trialByDisplay);

        var amountMinor = amountByName > 0 ? amountByName : amountByDisplay > 0 ? amountByDisplay : (long?)null;
        var trialDays = trialByName > 0 ? trialByName : trialByDisplay > 0 ? trialByDisplay : (int?)null;

        return new BillingPlanResponse
        {
            Name = plan.Name,
            DisplayName = string.IsNullOrWhiteSpace(plan.DisplayName) ? plan.Name : plan.DisplayName,
            Description = plan.Description,
            SortOrder = plan.SortOrder,
            IsEnterprise = plan.IsEnterprise,
            IsCurrentPlan = isCurrentPlan,
            Currency = currency,
            MonthlyAmountMinor = amountMinor,
            TrialDays = trialDays,
            PropertyLimit = propertyLimit,
            TeamLimit = teamLimit,
            AiRequestLimit = aiRequestLimit,
            WhatsAppMessageLimit = whatsAppLimit
        };
    }

    private static long? ReadQuotaLimit(SubscriptionPlan plan, string quotaKey)
    {
        var entitlement = plan.Entitlements.FirstOrDefault(item => string.Equals(item.Key, quotaKey, StringComparison.Ordinal));
        if (entitlement is null || !entitlement.IsEnabled)
        {
            return null;
        }

        return entitlement.IsUnlimited ? null : entitlement.QuotaLimit;
    }

    private static string ResolveCurrency(string? countryCode, BillingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(countryCode)
            && options.CountryCurrencies.TryGetValue(countryCode.Trim().ToUpperInvariant(), out var countryCurrency)
            && !string.IsNullOrWhiteSpace(countryCurrency))
        {
            return countryCurrency.Trim().ToUpperInvariant();
        }

        return string.IsNullOrWhiteSpace(options.DefaultCurrency)
            ? "USD"
            : options.DefaultCurrency.Trim().ToUpperInvariant();
    }

    private static IReadOnlyCollection<string> ResolvePaymentMethods(string? countryCode, string providerName, BillingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(countryCode)
            && options.CountryPaymentMethods.TryGetValue(countryCode.Trim().ToUpperInvariant(), out var methods)
            && methods.Length > 0)
        {
            return methods;
        }

        if (string.Equals(providerName, "Stripe", StringComparison.OrdinalIgnoreCase))
        {
            return ["Card"];
        }

        if (string.Equals(countryCode, "KE", StringComparison.OrdinalIgnoreCase)
            && string.Equals(providerName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return ["Mpesa", "Card"];
        }

        return options.DefaultPaymentMethods.Length > 0
            ? options.DefaultPaymentMethods
            : ["Card"];
    }

    private static BillingPaymentOptionResponse MapPaymentOption(string key)
    {
        var normalized = key.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "mpesa" => new BillingPaymentOptionResponse
            {
                Key = "Mpesa",
                Label = "M-Pesa",
                Description = "Pay securely with M-Pesa mobile money."
            },
            "card" => new BillingPaymentOptionResponse
            {
                Key = "Card",
                Label = "Pay by Card",
                Description = "Pay using debit or credit card where supported."
            },
            _ => new BillingPaymentOptionResponse
            {
                Key = normalized,
                Label = normalized,
                Description = "Pay with the configured provider option."
            }
        };
    }

    private Task ApplyProviderSnapshotAsync(TenantSubscription subscription, BillingProviderSubscriptionSnapshot snapshot, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        subscription.ExternalSubscriptionId = snapshot.SubscriptionId;
        subscription.ExternalPriceId = snapshot.PriceId ?? subscription.ExternalPriceId;
        subscription.CurrentPeriodStartUtc = snapshot.CurrentPeriodStartUtc;
        subscription.CurrentPeriodEndUtc = snapshot.CurrentPeriodEndUtc;
        subscription.TrialEndsAtUtc = snapshot.TrialEndsAtUtc;
        subscription.CancelAtPeriodEnd = snapshot.CancelAtPeriodEnd;
        subscription.LastProviderEventCreatedAtUtc = snapshot.EventCreatedAtUtc;

        subscription.Status = MapSubscriptionStatus(
            subscription.CancelAtPeriodEnd ? "customer.subscription.updated" : string.Empty,
            JsonSerializer.SerializeToElement(new
            {
                status = snapshot.Status,
                cancel_at_period_end = snapshot.CancelAtPeriodEnd
            }));

        if (subscription.Status == SubscriptionStatus.Cancelled.ToStorageValue())
        {
            subscription.EndedAtUtc = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    private bool TryGetTenant(out Guid companyId, out string error)
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

    private async Task<(bool Success, string Error)> EnsureOwnerOrAdministratorAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return (false, error);
        }

        var userId = tenantContext.UserId ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            return (false, "Authenticated tenant context is required.");
        }

        var membership = await dbContext.OrganizationMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.CompanyId == companyId
                && item.UserId == userId
                && item.Status == OrganizationMemberStatus.Active.ToStorageValue(), cancellationToken);
        if (membership is null)
        {
            return (false, "Active organization membership is required.");
        }

        if (!OrganizationRoleExtensions.TryParse(membership.Role, out var role)
            || (role != OrganizationRole.Owner && role != OrganizationRole.Administrator))
        {
            return (false, "Only organization owners or administrators can perform billing actions.");
        }

        return (true, string.Empty);
    }
}