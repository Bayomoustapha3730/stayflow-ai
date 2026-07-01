using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
