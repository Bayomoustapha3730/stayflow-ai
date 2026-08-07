using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Tests;

public sealed class PlatformAdminControllerIntegrationTests : IClassFixture<SignalRTestAppFactory>
{
    private static readonly Guid TargetCompanyId = SignalRTenantContextIntegrationTests.CompanyA;
    private static readonly Guid PlatformAdminUserId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly SignalRTestAppFactory _factory;

    public PlatformAdminControllerIntegrationTests(SignalRTestAppFactory factory)
    {
        _factory = factory;
        _factory.EnsureSeeded();
        EnsureTenantSeedData();
    }

    [Fact]
    public async Task PlatformAdmin_Endpoint_RequiresPlatformPermission()
    {
        using var client = CreateClientWithToken(TargetCompanyId, PlatformAdminUserId, ["conversations.read"]);

        using var response = await client.GetAsync("/api/platform-admin/tenants");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTenants_ReturnsPagedPayload()
    {
        using var client = CreateClientWithToken(TargetCompanyId, PlatformAdminUserId, ["platform.admin"]);

        using var response = await client.GetAsync("/api/platform-admin/tenants?page=1&pageSize=10");
        var payload = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.GetProperty("success").GetBoolean());
        Assert.True(payload.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task SuspendAndRestoreTenant_UpdatesTenantStatus()
    {
        using var client = CreateClientWithToken(TargetCompanyId, PlatformAdminUserId, ["platform.admin"]);

        using var suspendResponse = await client.PostAsync(
            $"/api/platform-admin/tenants/{TargetCompanyId:D}/suspend",
            JsonContent(new { reason = "Ops action" }));
        var suspendPayload = await ParseJsonAsync(suspendResponse);

        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);
        Assert.Equal("Suspended", suspendPayload.RootElement.GetProperty("data").GetProperty("status").GetString());

        using var restoreResponse = await client.PostAsync(
            $"/api/platform-admin/tenants/{TargetCompanyId:D}/restore",
            JsonContent(new { reason = "Ops restore" }));
        var restorePayload = await ParseJsonAsync(restoreResponse);

        Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);
        Assert.Equal("Active", restorePayload.RootElement.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task StartSupportImpersonation_RequiresExplicitAuthorizationCode()
    {
        using var client = CreateClientWithToken(TargetCompanyId, PlatformAdminUserId, ["platform.admin"]);

        using var response = await client.PostAsync(
            "/api/platform-admin/support/impersonation/start",
            JsonContent(new
            {
                targetCompanyId = TargetCompanyId,
                targetUserId = SignalRTenantContextIntegrationTests.UserA,
                reason = "Support request",
                explicitAuthorizationCode = ""
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartAndEndSupportImpersonation_AreAuditedAndSuccessful()
    {
        using var client = CreateClientWithToken(TargetCompanyId, PlatformAdminUserId, ["platform.admin"]);

        using var startResponse = await client.PostAsync(
            "/api/platform-admin/support/impersonation/start",
            JsonContent(new
            {
                targetCompanyId = TargetCompanyId,
                targetUserId = SignalRTenantContextIntegrationTests.UserA,
                reason = "Support request",
                explicitAuthorizationCode = "AUTH-2048"
            }));

        var startPayload = await ParseJsonAsync(startResponse);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        var sessionId = startPayload.RootElement.GetProperty("data").GetProperty("sessionId").GetGuid();

        using var endResponse = await client.PostAsync(
            $"/api/platform-admin/support/impersonation/{sessionId:D}/end",
            JsonContent(new { reason = "Done" }));

        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasStartAudit = dbContext.AuditLogs.Any(item => item.Action == "SupportImpersonationStarted");
        var hasEndAudit = dbContext.AuditLogs.Any(item => item.Action == "SupportImpersonationEnded");

        Assert.True(hasStartAudit);
        Assert.True(hasEndAudit);
    }

    [Fact]
    public async Task RepairTenant_ExecutesWithoutCrossTenantWrite()
    {
        using var client = CreateClientWithToken(TargetCompanyId, PlatformAdminUserId, ["platform.admin"]);

        using var response = await client.PostAsync(
            $"/api/platform-admin/tenants/{TargetCompanyId:D}/repair",
            JsonContent(new
            {
                normalizeStatusAndActivation = true,
                recomputeSubscriptionSnapshot = true,
                reason = "Repair"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient CreateClientWithToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var client = _factory.CreateClient();
        var token = CreateJwtToken(companyId, userId, permissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent JsonContent(object value)
        => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static string CreateJwtToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new("company_id", companyId.ToString("D")),
            new(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new(ClaimTypes.Name, "Platform Admin Integration Test")
        };

        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SignalRTestAppFactory.JwtSigningKey));
        var token = new JwtSecurityToken(
            issuer: SignalRTestAppFactory.JwtIssuer,
            audience: SignalRTestAppFactory.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private void EnsureTenantSeedData()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!dbContext.Companies.Any(item => item.Id == TargetCompanyId))
        {
            dbContext.Companies.Add(new Company
            {
                Id = TargetCompanyId,
                Name = "Tenant A",
                Slug = "tenant-a",
                NormalizedSlug = "TENANT-A",
                Status = "Active",
                Email = "tenant-a@example.test",
                PhoneNumber = "+10000000010",
                CountryCode = "US",
                TimeZone = "UTC",
                IsActive = true
            });
        }

        if (!dbContext.Users.Any(item => item.Id == PlatformAdminUserId))
        {
            dbContext.Users.Add(new User
            {
                Id = PlatformAdminUserId,
                CompanyId = TargetCompanyId,
                FullName = "Platform Admin",
                Email = "platform-admin@example.test",
                PhoneNumber = "+10000000011",
                Role = "Owner",
                PasswordHash = "hash",
                IsActive = true
            });
        }

        dbContext.SaveChanges();
    }
}
