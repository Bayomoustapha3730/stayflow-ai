using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ApiKeys;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class TenantApiKeyService(
    ApplicationDbContext dbContext,
    ICurrentTenantContext tenantContext,
    IPasswordHasher passwordHasher) : ITenantApiKeyService
{
    public async Task<ApiResponse<IReadOnlyCollection<TenantApiKeyDto>>> ListAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out _, out var error))
        {
            return ApiResponse<IReadOnlyCollection<TenantApiKeyDto>>.Fail(error);
        }

        var items = await dbContext.TenantApiKeys
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new TenantApiKeyDto
            {
                Id = item.Id,
                Name = item.Name,
                KeyPrefix = item.KeyPrefix,
                Scopes = item.ScopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                IsRevoked = item.IsRevoked,
                RevokedAtUtc = item.RevokedAtUtc,
                ExpiresAtUtc = item.ExpiresAtUtc,
                LastUsedAtUtc = item.LastUsedAtUtc,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<IReadOnlyCollection<TenantApiKeyDto>>.Ok(items);
    }

    public async Task<ApiResponse<CreateTenantApiKeyResponse>> CreateAsync(CreateTenantApiKeyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out var userId, out var error))
        {
            return ApiResponse<CreateTenantApiKeyResponse>.Fail(error);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<CreateTenantApiKeyResponse>.Fail("API key name is required.");
        }

        var scopes = request.Scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (scopes.Count == 0)
        {
            return ApiResponse<CreateTenantApiKeyResponse>.Fail("At least one API key scope is required.");
        }

        if (scopes.Any(scope => !ApiKeyScope.All.Contains(scope, StringComparer.Ordinal)))
        {
            return ApiResponse<CreateTenantApiKeyResponse>.Fail("One or more API key scopes are invalid.");
        }

        var prefix = GeneratePrefix();
        var secretPart = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        var plainKey = $"{prefix}.{secretPart}";
        var secretHash = passwordHasher.HashToken(plainKey);

        var apiKey = new TenantApiKey
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            CreatedByUserId = userId,
            Name = request.Name.Trim(),
            KeyPrefix = prefix,
            SecretHash = secretHash,
            ScopesCsv = string.Join(',', scopes),
            ExpiresAtUtc = request.ExpiresAtUtc
        };

        await dbContext.TenantApiKeys.AddAsync(apiKey, cancellationToken);
        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(TenantApiKey),
            EntityId = apiKey.Id,
            Action = "ApiKeyCreated",
            Details = $"{{\"companyId\":\"{companyId}\",\"name\":\"{apiKey.Name}\",\"scopes\":\"{apiKey.ScopesCsv}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<CreateTenantApiKeyResponse>.Ok(new CreateTenantApiKeyResponse
        {
            ApiKey = Map(apiKey),
            Secret = plainKey
        }, "API key created. Save the secret now because it will not be shown again.");
    }

    public async Task<ApiResponse<object>> RevokeAsync(Guid keyId, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var companyId, out _, out var error))
        {
            return ApiResponse<object>.Fail(error);
        }

        var item = await dbContext.TenantApiKeys
            .FirstOrDefaultAsync(apiKey => apiKey.Id == keyId && apiKey.CompanyId == companyId, cancellationToken);
        if (item is null)
        {
            return ApiResponse<object>.Fail("API key was not found.");
        }

        if (!item.IsRevoked)
        {
            item.IsRevoked = true;
            item.RevokedAtUtc = DateTimeOffset.UtcNow;

            await dbContext.AuditLogs.AddAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityName = nameof(TenantApiKey),
                EntityId = item.Id,
                Action = "ApiKeyRevoked",
                Details = $"{{\"companyId\":\"{companyId}\",\"name\":\"{item.Name}\"}}",
                CreatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ApiResponse<object>.Ok(new { keyId = item.Id }, "API key revoked.");
    }

    public async Task<TenantApiKeyValidationResult> ValidateAsync(string presentedKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(presentedKey) || !presentedKey.Contains('.', StringComparison.Ordinal))
        {
            return new TenantApiKeyValidationResult(false, null, null, null, [], "API key format is invalid.");
        }

        var splitIndex = presentedKey.IndexOf('.', StringComparison.Ordinal);
        var prefix = splitIndex <= 0 ? string.Empty : presentedKey[..splitIndex];
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return new TenantApiKeyValidationResult(false, null, null, null, [], "API key format is invalid.");
        }

        var item = await dbContext.TenantApiKeys
            .FirstOrDefaultAsync(apiKey => apiKey.KeyPrefix == prefix, cancellationToken);
        if (item is null)
        {
            return new TenantApiKeyValidationResult(false, null, null, null, [], "API key was not found.");
        }

        if (item.IsRevoked)
        {
            return new TenantApiKeyValidationResult(false, item, item.CompanyId, item.CreatedByUserId, [], "API key is revoked.");
        }

        if (item.ExpiresAtUtc is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
        {
            return new TenantApiKeyValidationResult(false, item, item.CompanyId, item.CreatedByUserId, [], "API key has expired.");
        }

        var presentedHash = passwordHasher.HashToken(presentedKey.Trim());
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(item.SecretHash),
                Convert.FromHexString(presentedHash)))
        {
            return new TenantApiKeyValidationResult(false, item, item.CompanyId, item.CreatedByUserId, [], "API key is invalid.");
        }

        item.LastUsedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var scopes = item.ScopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new TenantApiKeyValidationResult(true, item, item.CompanyId, item.CreatedByUserId, scopes, string.Empty);
    }

    public Task<bool> HasScopeAsync(ClaimsPrincipal principal, string scope, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(principal.Claims.Any(claim => claim.Type == "api_scope" && string.Equals(claim.Value, scope, StringComparison.Ordinal)));
    }

    private bool TryGetTenant(out Guid companyId, out Guid userId, out string error)
    {
        companyId = tenantContext.CompanyId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;

        if (!tenantContext.IsAuthenticated || companyId == Guid.Empty || userId == Guid.Empty)
        {
            error = "Authenticated tenant context is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string GeneratePrefix()
    {
        return "sfk_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
    }

    private static TenantApiKeyDto Map(TenantApiKey item)
    {
        return new TenantApiKeyDto
        {
            Id = item.Id,
            Name = item.Name,
            KeyPrefix = item.KeyPrefix,
            Scopes = item.ScopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            IsRevoked = item.IsRevoked,
            RevokedAtUtc = item.RevokedAtUtc,
            ExpiresAtUtc = item.ExpiresAtUtc,
            LastUsedAtUtc = item.LastUsedAtUtc,
            CreatedAt = item.CreatedAt
        };
    }
}