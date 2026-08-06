using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Tests;

public sealed class AuthControllerIntegrationTests : IClassFixture<SignalRTestAppFactory>
{
    private static readonly Guid DemoCompanyId = SeedData.DemoCompanyId;
    private static readonly Guid DemoUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly SignalRTestAppFactory factory;

    public AuthControllerIntegrationTests(SignalRTestAppFactory factory)
    {
        this.factory = factory;
        this.factory.EnsureSeeded();
        EnsureUserExists();
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUserProfile()
    {
        using var client = CreateClientWithToken(DemoCompanyId, DemoUserId, ["auth.me"]);

        using var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("demo.user@stayflow.local", payload.RootElement.GetProperty("data").GetProperty("email").GetString());
        Assert.True(payload.RootElement.GetProperty("data").TryGetProperty("preferredLanguage", out _));
        Assert.True(payload.RootElement.GetProperty("data").TryGetProperty("timeZone", out _));
    }

    [Fact]
    public async Task UpdateMe_WithValidToken_UpdatesProfile()
    {
        using var client = CreateClientWithToken(DemoCompanyId, DemoUserId, ["auth.me"]);

        using var response = await client.PutAsJsonAsync("/auth/me", new
        {
            fullName = "Updated Demo User",
            phoneNumber = "+254700001234",
            preferredLanguage = "fr",
            timeZone = "Africa/Nairobi",
            emailNotificationsEnabled = true,
            securityNotificationsEnabled = true,
            productUpdatesEnabled = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = payload.RootElement.GetProperty("data");
        Assert.Equal("Updated Demo User", data.GetProperty("fullName").GetString());
        Assert.Equal("fr", data.GetProperty("preferredLanguage").GetString());
        Assert.Equal("Africa/Nairobi", data.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task PasswordReset_ForUnknownEmail_ReturnsGenericSuccess()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/auth/password-reset", new
        {
            email = "missing@example.test"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("If the account exists, a password reset token has been generated.", payload.RootElement.GetProperty("message").GetString());
    }

    private HttpClient CreateClientWithToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var client = factory.CreateClient();
        var token = CreateJwtToken(companyId, userId, permissions);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private void EnsureUserExists()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (dbContext.Users.Any(user => user.Id == DemoUserId))
        {
            return;
        }

        dbContext.Users.Add(new User
        {
            Id = DemoUserId,
            CompanyId = DemoCompanyId,
            FullName = "Demo User",
            Email = "demo.user@stayflow.local",
            PhoneNumber = "+254700000001",
            PreferredLanguage = "en",
            TimeZone = "UTC",
            Role = "Administrator",
            PasswordHash = "integration-only-placeholder",
            IsEmailVerified = true,
            IsActive = true,
            EmailNotificationsEnabled = true,
            SecurityNotificationsEnabled = true,
            ProductUpdatesEnabled = false
        });

        dbContext.SaveChanges();
    }

    private static string CreateJwtToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new("company_id", companyId.ToString("D")),
            new("session_id", Guid.NewGuid().ToString("D")),
            new(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new(ClaimTypes.Name, "Auth Integration Test")
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