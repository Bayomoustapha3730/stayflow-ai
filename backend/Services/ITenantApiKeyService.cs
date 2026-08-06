using System.Security.Claims;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.ApiKeys;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface ITenantApiKeyService
{
    Task<ApiResponse<IReadOnlyCollection<TenantApiKeyDto>>> ListAsync(CancellationToken cancellationToken);
    Task<ApiResponse<CreateTenantApiKeyResponse>> CreateAsync(CreateTenantApiKeyRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<object>> RevokeAsync(Guid keyId, CancellationToken cancellationToken);

    Task<TenantApiKeyValidationResult> ValidateAsync(string presentedKey, CancellationToken cancellationToken);
    Task<bool> HasScopeAsync(ClaimsPrincipal principal, string scope, CancellationToken cancellationToken);
}

public sealed record TenantApiKeyValidationResult(
    bool Success,
    TenantApiKey? ApiKey,
    Guid? CompanyId,
    Guid? CreatedByUserId,
    IReadOnlyCollection<string> Scopes,
    string FailureReason);