using System.Security.Claims;
using StayFlow.Api.Middleware;

namespace StayFlow.Api.Services;

public sealed class CurrentTenantContext(
    IHttpContextAccessor httpContextAccessor,
    ITenantExecutionContextAccessor tenantExecutionContextAccessor)
    : TenantContext(httpContextAccessor, tenantExecutionContextAccessor)
{
}
