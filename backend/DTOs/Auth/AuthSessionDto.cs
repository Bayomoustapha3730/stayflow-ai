namespace StayFlow.Api.DTOs.Auth;

public sealed class AuthSessionDto
{
    public Guid SessionId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastUsedAtUtc { get; init; }
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public bool IsCurrent { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}