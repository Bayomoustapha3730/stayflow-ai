using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Billing;

namespace StayFlow.Api.Services;

public interface IBillingService
{
    Task<ApiResponse<CreateCheckoutSessionResponse>> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<CreateBillingPortalSessionResponse>> CreateBillingPortalSessionAsync(CancellationToken cancellationToken);
    Task<ApiResponse<CreateBillingPortalSessionResponse>> CreatePaymentMethodManagementSessionAsync(CancellationToken cancellationToken);
    Task<ApiResponse<BillingSubscriptionResponse>> GetSubscriptionAsync(CancellationToken cancellationToken);
    Task<ApiResponse<BillingSubscriptionResponse>> ChangeSubscriptionPlanAsync(ChangeSubscriptionPlanRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<BillingSubscriptionResponse>> CancelSubscriptionAsync(CancelSubscriptionRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<BillingSubscriptionResponse>> ResumeSubscriptionAsync(CancellationToken cancellationToken);
    Task<ApiResponse<IReadOnlyCollection<TenantInvoiceDto>>> GetInvoicesAsync(CancellationToken cancellationToken);
    Task<ApiResponse<UsageSummaryResponse>> GetUsageSummaryAsync(CancellationToken cancellationToken);
    Task<BillingWebhookProcessingResult> ProcessStripeWebhookAsync(string rawBody, string signatureHeader, CancellationToken cancellationToken);
}