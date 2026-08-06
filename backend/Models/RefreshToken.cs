namespace StayFlow.Api.Models;

public sealed class RefreshToken : AuditableEntity
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIpAddress { get; set; }
    public string? CreatedByUserAgent { get; set; }
    public bool IsRevoked => RevokedAt is not null;

    public User User { get; set; } = null!;
}
