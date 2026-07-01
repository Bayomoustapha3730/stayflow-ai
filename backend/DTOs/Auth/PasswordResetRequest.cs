namespace StayFlow.Api.DTOs.Auth;

public sealed class PasswordResetRequest
{
    public string Email { get; init; } = string.Empty;
}
