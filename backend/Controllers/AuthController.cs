using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

/// <summary>
/// Handles JWT authentication, refresh tokens, password reset, and email verification.
/// </summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
[EnableRateLimiting("public-auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Authenticates a user and returns access and refresh tokens.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthTokenResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthTokenResponse>>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Rotates a refresh token and returns a new token pair.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AuthTokenResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthTokenResponse>>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.RefreshAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Generates a password reset token.
    /// </summary>
    [HttpPost("password-reset")]
    [EnableRateLimiting("password-reset-request")]
    public async Task<ActionResult<ApiResponse<object>>> RequestPasswordReset(
        PasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.RequestPasswordResetAsync(request, cancellationToken));
    }

    /// <summary>
    /// Confirms a password reset token and updates the password.
    /// </summary>
    [HttpPost("password-reset/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmPasswordReset(
        PasswordResetConfirmRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.ConfirmPasswordResetAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Changes the authenticated user's password.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.ChangePasswordAsync(User, request, cancellationToken);
        return AuthenticatedResult(response);
    }

    /// <summary>
    /// Generates a new email verification token for the authenticated user.
    /// </summary>
    [Authorize]
    [HttpPost("email-verification")]
    [EnableRateLimiting("verification-resend")]
    [ProducesResponseType(typeof(ApiResponse<EmailVerificationChallengeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmailVerificationChallengeDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<EmailVerificationChallengeDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<EmailVerificationChallengeDto>>> RequestEmailVerification(CancellationToken cancellationToken)
    {
        var response = await authService.RequestEmailVerificationAsync(User, cancellationToken);
        return AuthenticatedResult(response);
    }

    /// <summary>
    /// Resends an email verification token for the authenticated user.
    /// </summary>
    [Authorize]
    [HttpPost("email-verification/resend")]
    [EnableRateLimiting("verification-resend")]
    [ProducesResponseType(typeof(ApiResponse<EmailVerificationChallengeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmailVerificationChallengeDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<EmailVerificationChallengeDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<EmailVerificationChallengeDto>>> ResendEmailVerification(CancellationToken cancellationToken)
    {
        var response = await authService.RequestEmailVerificationAsync(User, cancellationToken);
        return AuthenticatedResult(response);
    }

    /// <summary>
    /// Confirms an email verification token.
    /// </summary>
    [HttpPost("email-verification/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmEmailVerification(
        EmailVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.ConfirmEmailVerificationAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Gets the authenticated user profile, roles, and permissions.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> Me(CancellationToken cancellationToken)
    {
        var response = await authService.GetCurrentUserAsync(User, cancellationToken);
        return response.Success ? Ok(response) : Unauthorized(response);
    }

    /// <summary>
    /// Updates the authenticated user's profile.
    /// </summary>
    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> UpdateMe(
        UpdateCurrentUserRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.UpdateCurrentUserAsync(User, request, cancellationToken);
        return AuthenticatedResult(response);
    }

    [Authorize]
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<AuthSessionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<AuthSessionDto>>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AuthSessionDto>>>> Sessions(CancellationToken cancellationToken)
    {
        var response = await authService.GetSessionsAsync(User, cancellationToken);
        return response.Success ? Ok(response) : Unauthorized(response);
    }

    [Authorize]
    [HttpPost("sessions/{sessionId:guid}/revoke")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var response = await authService.RevokeSessionAsync(User, sessionId, cancellationToken);
        return AuthenticatedResult(response);
    }

    [Authorize]
    [HttpPost("sessions/revoke-all")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> RevokeAllSessions(CancellationToken cancellationToken)
    {
        var response = await authService.RevokeAllSessionsAsync(User, cancellationToken);
        return AuthenticatedResult(response);
    }

    private ActionResult<ApiResponse<T>> AuthenticatedResult<T>(ApiResponse<T> response)
    {
        if (response.Success)
        {
            return Ok(response);
        }

        return string.Equals(response.Message, "Current user is not available.", StringComparison.Ordinal)
            ? Unauthorized(response)
            : BadRequest(response);
    }
}
