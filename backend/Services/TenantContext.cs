using System.Security.Claims;
using StayFlow.Api.Middleware;

namespace StayFlow.Api.Services;

public class TenantContext(
    IHttpContextAccessor httpContextAccessor,
    ITenantExecutionContextAccessor tenantExecutionContextAccessor) : ICurrentTenantContext, ITenantContext
{
    private static readonly string[] CompanyClaimTypes = ["company_id", "tenant_id"];

    public Guid? TenantId => CompanyId;

    public Guid? CompanyId => TryGetGuidClaim(CompanyClaimTypes);

    public Guid? UserId => TryGetGuidClaim([ClaimTypes.NameIdentifier]);

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

    private Guid? TryGetGuidClaim(IEnumerable<string> claimTypes)
    {
        if (tenantExecutionContextAccessor.IsAuthenticated)
        {
            var requestedType = claimTypes.FirstOrDefault();
            return requestedType == "company_id" || requestedType == "tenant_id"
                ? tenantExecutionContextAccessor.CompanyId
                : requestedType == ClaimTypes.NameIdentifier
                    ? tenantExecutionContextAccessor.UserId
                    : null;
        }

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirstValue(claimType);
            if (Guid.TryParse(value, out var id) && id != Guid.Empty)
            {
                return id;
            }
        }

        return null;
    }
}