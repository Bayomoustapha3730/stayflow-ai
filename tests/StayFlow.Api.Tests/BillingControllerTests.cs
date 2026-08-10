using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.Controllers;
using StayFlow.Api.DTOs.Billing;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Tests;

public sealed class BillingControllerTests
{
    [Fact]
    public async Task StripeWebhook_WithMissingSignature_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeBillingService());
        ConfigureRequest(controller, "{}", null);

        var result = await controller.StripeWebhook(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task StripeWebhook_WithOversizedPayload_ReturnsPayloadTooLarge()
    {
        var controller = CreateController(new FakeBillingService(), maxBodyBytes: 10);
        ConfigureRequest(controller, "12345678901", "t=1,v1=abc");

        var result = await controller.StripeWebhook(CancellationToken.None);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, status.StatusCode);
    }

    [Fact]
    public async Task GetUsage_ReturnsBadRequest_OnServiceFailure()
    {
        var service = new FakeBillingService
        {
            UsageResponse = ApiResponse<UsageSummaryResponse>.Fail("not allowed")
        };
        var controller = CreateController(service);

        var result = await controller.GetUsage(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CancelSubscription_ReturnsOk_OnServiceSuccess()
    {
        var service = new FakeBillingService
        {
            CancelResponse = ApiResponse<BillingSubscriptionResponse>.Ok(new BillingSubscriptionResponse
            {
                CompanyId = Guid.NewGuid(),
                Status = "CancelAtPeriodEnd",
                CancelAtPeriodEnd = true,
                CurrentPeriodStartUtc = DateTimeOffset.UtcNow.AddDays(-3),
                CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(27)
            })
        };
        var controller = CreateController(service);

        var result = await controller.CancelSubscription(new CancelSubscriptionRequest { AtPeriodEnd = true }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    private static BillingController CreateController(IBillingService service, int maxBodyBytes = 1024)
    {
        var options = Options.Create(new BillingOptions
        {
            WebhookMaxBodyBytes = maxBodyBytes
        });

        return new BillingController(service, options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static void ConfigureRequest(BillingController controller, string body, string? signature)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(bytes);
        controller.ControllerContext.HttpContext.Request.ContentLength = bytes.Length;
        if (signature is not null)
        {
            controller.ControllerContext.HttpContext.Request.Headers["Stripe-Signature"] = signature;
        }
    }

    private sealed class FakeBillingService : IBillingService
    {
        public ApiResponse<UsageSummaryResponse> UsageResponse { get; init; } = ApiResponse<UsageSummaryResponse>.Ok(new UsageSummaryResponse
        {
            CompanyId = Guid.NewGuid(),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Metrics = []
        });

        public ApiResponse<BillingSubscriptionResponse> CancelResponse { get; init; } = ApiResponse<BillingSubscriptionResponse>.Fail("no-op");

        public Task<ApiResponse<CreateCheckoutSessionResponse>> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<CreateCheckoutSessionResponse>.Fail("not implemented"));

        public Task<ApiResponse<CreateBillingPortalSessionResponse>> CreateBillingPortalSessionAsync(CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<CreateBillingPortalSessionResponse>.Fail("not implemented"));

        public Task<ApiResponse<CreateBillingPortalSessionResponse>> CreatePaymentMethodManagementSessionAsync(CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<CreateBillingPortalSessionResponse>.Fail("not implemented"));

        public Task<ApiResponse<BillingSubscriptionResponse?>> GetSubscriptionAsync(CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<BillingSubscriptionResponse?>.Ok(null));

        public Task<ApiResponse<IReadOnlyCollection<BillingPlanResponse>>> GetPlansAsync(CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<IReadOnlyCollection<BillingPlanResponse>>.Ok([]));

        public Task<ApiResponse<IReadOnlyCollection<BillingPaymentOptionResponse>>> GetPaymentOptionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<IReadOnlyCollection<BillingPaymentOptionResponse>>.Ok([]));

        public Task<ApiResponse<BillingSubscriptionResponse>> ChangeSubscriptionPlanAsync(ChangeSubscriptionPlanRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<BillingSubscriptionResponse>.Fail("not implemented"));

        public Task<ApiResponse<BillingSubscriptionResponse>> CancelSubscriptionAsync(CancelSubscriptionRequest request, CancellationToken cancellationToken)
            => Task.FromResult(CancelResponse);

        public Task<ApiResponse<BillingSubscriptionResponse>> ResumeSubscriptionAsync(CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<BillingSubscriptionResponse>.Fail("not implemented"));

        public Task<ApiResponse<IReadOnlyCollection<TenantInvoiceDto>>> GetInvoicesAsync(CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<IReadOnlyCollection<TenantInvoiceDto>>.Ok([]));

        public Task<ApiResponse<UsageSummaryResponse>> GetUsageSummaryAsync(CancellationToken cancellationToken)
            => Task.FromResult(UsageResponse);

        public Task<BillingWebhookProcessingResult> ProcessStripeWebhookAsync(string rawBody, string signatureHeader, CancellationToken cancellationToken)
            => Task.FromResult(new BillingWebhookProcessingResult
            {
                EventId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))),
                EventType = "test",
                WasDuplicate = false,
                AppliedStateChange = false
            });
    }
}
