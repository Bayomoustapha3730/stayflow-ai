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
            tenantContext.CorrelationId), cancellationToken);

        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantSubscription),
            EntityId = companyId,
            Action = "BillingCheckoutCreated",
            Details = $"{{\"companyId\":\"{companyId}\",\"plan\":\"{planName}\"}}",
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

    public async Task<ApiResponse<BillingSubscriptionResponse>> GetSubscriptionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail(error);
        }

        var subscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .Include(item => item.SubscriptionPlan)
            .OrderByDescending(item => item.CurrentPeriodStartUtc)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        if (subscription is null)
        {
            return ApiResponse<BillingSubscriptionResponse>.Fail("Subscription was not found.");
        }

        return ApiResponse<BillingSubscriptionResponse>.Ok(new BillingSubscriptionResponse
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
        });
    }

    public async Task<ApiResponse<IReadOnlyCollection<TenantInvoiceDto>>> GetInvoicesAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var error))
        {
            return ApiResponse<IReadOnlyCollection<TenantInvoiceDto>>.Fail(error);
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

    public async Task<BillingWebhookProcessingResult> ProcessStripeWebhookAsync(string rawBody, string signatureHeader, CancellationToken cancellationToken)
    {
        var envelope = billingProvider.ValidateAndParseWebhook(rawBody, signatureHeader);

        var existing = await dbContext.BillingWebhookEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Provider == billingProvider.ProviderName && item.EventId == envelope.EventId, cancellationToken);
        if (existing is not null)
        {
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
        var customerId = envelope.CustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return false;
        }

        var company = await dbContext.Companies
            .FirstOrDefaultAsync(item => item.StripeCustomerId == customerId, cancellationToken);
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