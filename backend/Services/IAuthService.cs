using System.Security.Claims;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Auth;

namespace StayFlow.Api.Services;

public interface IAuthService
{
    Task<ApiResponse<AuthTokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<AuthTokenResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<object>> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<object>> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<object>> ConfirmEmailVerificationAsync(EmailVerificationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
