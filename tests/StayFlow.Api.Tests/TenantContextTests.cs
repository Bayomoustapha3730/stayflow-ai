using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class TenantContextTests
{
    [Fact]
    public void CompanyId_ResolvesFromCompanyIdClaim_WhenAuthenticated()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
            new Claim("company_id", Guid.Parse("11111111-1111-1111-1111-111111111111").ToString("D"))
        ], "TestAuth"));

        var tenantContext = new TenantContext(new HttpContextAccessor { HttpContext = httpContext }, new TenantExecutionContextAccessor());

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), tenantContext.CompanyId);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), tenantContext.TenantId);
    }

    [Fact]
    public void CompanyId_ResolvesFromTenantIdAlias_WhenCompanyIdClaimMissing()
    {
        var tenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
            new Claim("tenant_id", tenantId.ToString("D"))
        ], "TestAuth"));

        var tenantContext = new TenantContext(new HttpContextAccessor { HttpContext = httpContext }, new TenantExecutionContextAccessor());

        Assert.Equal(tenantId, tenantContext.CompanyId);
    }

    [Fact]
    public void CompanyId_ReturnsNull_WhenNoTenantClaimExists()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D"))
        ], "TestAuth"));

        var tenantContext = new TenantContext(new HttpContextAccessor { HttpContext = httpContext }, new TenantExecutionContextAccessor());

        Assert.Null(tenantContext.CompanyId);
    }
}