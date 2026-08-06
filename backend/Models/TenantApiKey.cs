namespace StayFlow.Api.Models;

public sealed class TenantApiKey : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public string ScopesCsv { get; set; } = string.Empty;
    public bool IsRevoked { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }

    public Company Company { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}