namespace StayFlow.Api.DTOs.Auth;

public sealed class PasswordResetConfirmRequest
{
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
