namespace StayFlow.Api.DTOs.ApiKeys;

public sealed class TenantApiKeyDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string KeyPrefix { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Scopes { get; init; } = [];
    public bool IsRevoked { get; init; }
    public DateTimeOffset? RevokedAtUtc { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public DateTimeOffset? LastUsedAtUtc { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreateTenantApiKeyRequest
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Scopes { get; init; } = [];
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}

public sealed class CreateTenantApiKeyResponse
{
    public TenantApiKeyDto ApiKey { get; init; } = new();
    public string Secret { get; init; } = string.Empty;
}