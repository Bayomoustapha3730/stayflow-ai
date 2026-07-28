using System.Security.Claims;
using StayFlow.Api.Middleware;

namespace StayFlow.Api.Services;

public sealed class CurrentTenantContext(
    IHttpContextAccessor httpContextAccessor,
    ITenantExecutionContextAccessor tenantExecutionContextAccessor) : ICurrentTenantContext
{
    private const string CompanyIdClaimType = "company_id";

    public Guid? CompanyId => TryGetGuidClaim(CompanyIdClaimType);

    public Guid? UserId => TryGetGuidClaim(ClaimTypes.NameIdentifier);

    public string? CorrelationId
    {
        get
        {
            if (tenantExecutionContextAccessor.IsAuthenticated)
            {
                return tenantExecutionContextAccessor.CorrelationId;
            }

            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return null;
            }

            return httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
                ? value?.ToString()
                : httpContext.TraceIdentifier;
        }
    }

    public bool IsAuthenticated => tenantExecutionContextAccessor.IsAuthenticated || httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    private Guid? TryGetGuidClaim(string claimType)
    {
        if (tenantExecutionContextAccessor.IsAuthenticated)
        {
            return claimType == CompanyIdClaimType
                ? tenantExecutionContextAccessor.CompanyId
                : claimType == ClaimTypes.NameIdentifier
                    ? tenantExecutionContextAccessor.UserId
                    : null;
        }

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var value = user.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
