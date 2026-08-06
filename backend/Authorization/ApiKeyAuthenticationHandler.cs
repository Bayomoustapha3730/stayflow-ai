using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using StayFlow.Api.Services;

namespace StayFlow.Api.Authorization;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ITenantApiKeyService tenantApiKeyService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var header = headerValues.ToString();
        if (!header.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var key = header["ApiKey ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return AuthenticateResult.Fail("API key was not provided.");
        }

        var validation = await tenantApiKeyService.ValidateAsync(key, Context.RequestAborted);
        if (!validation.Success || validation.ApiKey is null || validation.CompanyId is null)
        {
            return AuthenticateResult.Fail(validation.FailureReason);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, validation.CreatedByUserId?.ToString() ?? Guid.Empty.ToString()),
            new("company_id", validation.CompanyId.Value.ToString()),
            new("tenant_id", validation.CompanyId.Value.ToString()),
            new("auth_method", "api_key"),
            new("apikey_id", validation.ApiKey.Id.ToString())
        };

        claims.AddRange(validation.Scopes.Select(scope => new Claim("api_scope", scope)));

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }
}