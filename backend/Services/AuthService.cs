using System.Security.Claims;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class AuthService(
    IAuthRepository authRepository,
    IJwtTokenService jwtTokenService,
    IPasswordHasher passwordHasher,
    IConfiguration configuration) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<ApiResponse<AuthTokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authRepository.GetUserByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null)
        {
            return ApiResponse<AuthTokenResponse>.Fail("Invalid email or password.");
        }

        if (user.LockoutEndAt > DateTimeOffset.UtcNow)
        {
            return ApiResponse<AuthTokenResponse>.Fail("Account is temporarily locked.");
        }

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEndAt = DateTimeOffset.UtcNow.Add(LockoutDuration);
            }

            await authRepository.SaveChangesAsync(cancellationToken);
            return ApiResponse<AuthTokenResponse>.Fail("Invalid email or password.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = DateTimeOffset.UtcNow;

        var response = jwtTokenService.CreateTokenResponse(user, GetRoles(user), GetPermissions(user));
        await authRepository.AddRefreshTokenAsync(CreateRefreshToken(user.Id, response.RefreshToken), cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<AuthTokenResponse>.Ok(response, "Login successful.");
    }

    public async Task<ApiResponse<AuthTokenResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = passwordHasher.HashToken(request.RefreshToken);
        var refreshToken = await authRepository.GetRefreshTokenAsync(tokenHash, cancellationToken);
        if (refreshToken is null || refreshToken.IsRevoked || refreshToken.ExpiresAt <= DateTimeOffset.UtcNow || !refreshToken.User.IsActive)
        {
            return ApiResponse<AuthTokenResponse>.Fail("Refresh token is invalid.");
        }

        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        var response = jwtTokenService.CreateTokenResponse(refreshToken.User, GetRoles(refreshToken.User), GetPermissions(refreshToken.User));
        await authRepository.AddRefreshTokenAsync(CreateRefreshToken(refreshToken.UserId, response.RefreshToken), cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<AuthTokenResponse>.Ok(response, "Token refreshed successfully.");
    }

    public async Task<ApiResponse<object>> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var user = await authRepository.GetUserByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null)
        {
            return ApiResponse<object>.Ok(new { }, "If the account exists, a password reset token has been generated.");
        }

        var resetToken = passwordHasher.GenerateSecureToken();
        await authRepository.AddPasswordResetTokenAsync(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = passwordHasher.HashToken(resetToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        }, cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { ResetToken = resetToken }, "Password reset token generated.");
    }

    public async Task<ApiResponse<object>> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, CancellationToken cancellationToken)
    {
        var token = await authRepository.GetPasswordResetTokenAsync(passwordHasher.HashToken(request.Token), cancellationToken);
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ApiResponse<object>.Fail("Password reset token is invalid.");
        }

        if (request.NewPassword.Length < 12)
        {
            return ApiResponse<object>.Fail("Password must be at least 12 characters.");
        }

        token.User.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        token.User.FailedLoginAttempts = 0;
        token.User.LockoutEndAt = null;
        token.UsedAt = DateTimeOffset.UtcNow;
        await authRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { }, "Password reset successfully.");
    }

    public async Task<ApiResponse<object>> ConfirmEmailVerificationAsync(EmailVerificationRequest request, CancellationToken cancellationToken)
    {
        var token = await authRepository.GetEmailVerificationTokenAsync(passwordHasher.HashToken(request.Token), cancellationToken);
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ApiResponse<object>.Fail("Email verification token is invalid.");
        }

        token.User.IsEmailVerified = true;
        token.UsedAt = DateTimeOffset.UtcNow;
        await authRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { }, "Email verified successfully.");
    }

    public async Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return ApiResponse<CurrentUserDto>.Fail("Current user is not available.");
        }

        var user = await authRepository.GetUserByIdAsync(userId, cancellationToken);
        return user is null
            ? ApiResponse<CurrentUserDto>.Fail("Current user is not available.")
            : ApiResponse<CurrentUserDto>.Ok(new CurrentUserDto
            {
                Id = user.Id,
                CompanyId = user.CompanyId,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsEmailVerified = user.IsEmailVerified,
                Roles = GetRoles(user),
                Permissions = GetPermissions(user)
            });
    }

    private RefreshToken CreateRefreshToken(Guid userId, string refreshToken)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = passwordHasher.HashToken(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(GetRefreshTokenDays())
        };
    }

    private int GetRefreshTokenDays()
    {
        return int.TryParse(configuration["Jwt:RefreshTokenDays"], out var days) ? days : 30;
    }

    private static IReadOnlyCollection<string> GetRoles(User user)
    {
        return user.UserRoles.Select(userRole => userRole.Role.Name).Distinct().ToList();
    }

    private static IReadOnlyCollection<string> GetPermissions(User user)
    {
        return user.UserRoles
            .SelectMany(userRole => userRole.Role.RolePermissions)
            .Select(rolePermission => rolePermission.Permission.Name)
            .Distinct()
            .ToList();
    }
}
