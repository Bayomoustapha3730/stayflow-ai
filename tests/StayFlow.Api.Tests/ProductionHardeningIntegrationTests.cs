using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace StayFlow.Api.Tests;

public sealed class ProductionHardeningIntegrationTests : IClassFixture<SignalRTestAppFactory>
{
    private readonly SignalRTestAppFactory factory;

    public ProductionHardeningIntegrationTests(SignalRTestAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Health_Endpoints_Return_Minimal_Status_Payload()
    {
        using var client = factory.CreateClient();

        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        var liveJson = JsonDocument.Parse(await live.Content.ReadAsStringAsync());
        var readyJson = JsonDocument.Parse(await ready.Content.ReadAsStringAsync());

        Assert.True(liveJson.RootElement.TryGetProperty("status", out _));
        Assert.True(readyJson.RootElement.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task CorrelationId_InvalidInboundValue_Is_Replaced_And_Emitted()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", new string('x', 120));

        using var response = await client.GetAsync("/api/status");

        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        var correlationId = Assert.Single(values);
        Assert.NotEqual(new string('x', 120), correlationId);
        Assert.InRange(correlationId.Length, 16, 64);
    }

    [Fact]
    public async Task PermissionFailure_Returns_ProblemDetails_With_Correlation_Metadata()
    {
        using var client = CreateClientWithToken(SignalRTenantContextIntegrationTests.CompanyA, SignalRTenantContextIntegrationTests.UserA, ["chat.read"]);

        using var response = await client.GetAsync("/conversations");
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(403, payload.RootElement.GetProperty("status").GetInt32());
        Assert.True(payload.RootElement.TryGetProperty("traceId", out _));
        Assert.True(payload.RootElement.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task Auth_Login_Is_Rate_Limited_With_429_ProblemDetails()
    {
        using var client = factory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (var index = 0; index < 40; index++)
        {
            lastResponse = await client.PostAsJsonAsync("/auth/login", new { email = "invalid@example.test", password = "bad" });
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);

        var payload = JsonDocument.Parse(await lastResponse.Content.ReadAsStringAsync());
        Assert.Equal(429, payload.RootElement.GetProperty("status").GetInt32());
        Assert.True(lastResponse.Headers.TryGetValues("Retry-After", out _));
    }

    private HttpClient CreateClientWithToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var client = factory.CreateClient();
        var token = CreateJwtToken(companyId, userId, permissions);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string CreateJwtToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new("company_id", companyId.ToString("D")),
            new(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new(ClaimTypes.Name, "Production Hardening Test")
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
}
