using StayFlow.Api.Authorization;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Services;

namespace StayFlow.Api.Middleware;

public sealed class FeatureEntitlementMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenantContext tenantContext, ISubscriptionEntitlementService subscriptionEntitlementService)
    {
        var requiredFeatures = context.GetEndpoint()?.Metadata
            .GetOrderedMetadata<RequireFeatureAttribute>()
            ?.Select(item => item.FeatureKey)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        if (requiredFeatures.Length > 0)
        {
            if (!tenantContext.IsAuthenticated || tenantContext.CompanyId is not { } companyId || companyId == Guid.Empty)
            {
                throw new ForbiddenOperationException("Authenticated tenant context is required.", "tenant_context_required");
            }

            foreach (var featureKey in requiredFeatures)
            {
                await subscriptionEntitlementService.EnsureFeatureEnabledAsync(companyId, featureKey, context.RequestAborted);
            }
        }

        await next(context);
    }
}