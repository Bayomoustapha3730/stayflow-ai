namespace StayFlow.Api.DTOs.Auth;

public sealed class EmailVerificationChallengeDto
{
    public string VerificationToken { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; init; }
}