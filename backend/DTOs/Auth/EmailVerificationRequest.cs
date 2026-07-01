namespace StayFlow.Api.DTOs.Auth;

public sealed class EmailVerificationRequest
{
    public string Token { get; init; } = string.Empty;
}
