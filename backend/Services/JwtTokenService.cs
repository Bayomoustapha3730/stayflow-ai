using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class JwtTokenService(IConfiguration configuration, IPasswordHasher passwordHasher) : IJwtTokenService
{
    public AuthTokenResponse CreateTokenResponse(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(GetAccessTokenMinutes());
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("company_id", user.CompanyId.ToString()),
            new("tenant_id", user.CompanyId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName)
        };

        var organizationRole = user.OrganizationMemberships
            .Where(membership => membership.CompanyId == user.CompanyId && membership.Status == Models.OrganizationMemberStatus.Active.ToStorageValue())
            .Select(membership => membership.Role)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(organizationRole))
        {
            claims.Add(new Claim("org_role", organizationRole));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new AuthTokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = passwordHasher.GenerateSecureToken(),
            ExpiresAt = expiresAt
        };
    }

    private int GetAccessTokenMinutes()
    {
        return int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var minutes) ? minutes : 30;
    }
}
