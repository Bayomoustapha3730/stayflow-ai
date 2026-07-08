using System.Security.Claims;
using StayFlow.Api.Middleware;

namespace StayFlow.Api.Services;

public sealed class CurrentTenantContext(IHttpContextAccessor httpContextAccessor) : ICurrentTenantContext
{
    private const string CompanyIdClaimType = "company_id";

    public Guid? CompanyId => TryGetGuidClaim(CompanyIdClaimType);

    public Guid? UserId => TryGetGuidClaim(ClaimTypes.NameIdentifier);

    public string? CorrelationId
    {
        get
        {
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

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    private Guid? TryGetGuidClaim(string claimType)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var value = user.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
