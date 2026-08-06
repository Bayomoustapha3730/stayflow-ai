using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services.Email;

namespace StayFlow.Api.Services;

public sealed class AuthService(
    IAuthRepository authRepository,
    IJwtTokenService jwtTokenService,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    IIdentityEmailService identityEmailService) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan EmailVerificationLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);

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

        var sessionId = Guid.NewGuid();
        var response = jwtTokenService.CreateTokenResponse(user, GetRoles(user), GetPermissions(user), sessionId);
        await authRepository.AddRefreshTokenAsync(CreateRefreshToken(user.Id, response.RefreshToken, sessionId), cancellationToken);
        await AddAuditLogAsync(user, "LoginSucceeded", new { sessionId }, cancellationToken);
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

        refreshToken.LastUsedAt = DateTimeOffset.UtcNow;
        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        refreshToken.RevokedReason = "Rotated";

        var response = jwtTokenService.CreateTokenResponse(refreshToken.User, GetRoles(refreshToken.User), GetPermissions(refreshToken.User), refreshToken.SessionId);
        var replacement = CreateRefreshToken(refreshToken.UserId, response.RefreshToken, refreshToken.SessionId);
        refreshToken.ReplacedByTokenId = replacement.Id;
        await authRepository.AddRefreshTokenAsync(replacement, cancellationToken);
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

        await authRepository.RevokeActivePasswordResetTokensAsync(user.Id, cancellationToken);

        var resetToken = GenerateUrlSafeToken();
        await authRepository.AddPasswordResetTokenAsync(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = passwordHasher.HashToken(resetToken),
            ExpiresAt = DateTimeOffset.UtcNow.Add(PasswordResetLifetime)
        }, cancellationToken);
        await identityEmailService.SendPasswordResetAsync(user.Email, user.FullName, resetToken, cancellationToken);
        await AddAuditLogAsync(user, "PasswordResetRequested", null, cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { }, "If the account exists, a password reset token has been generated.");
    }

    public async Task<ApiResponse<object>> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, CancellationToken cancellationToken)
    {
        var token = await authRepository.GetPasswordResetTokenAsync(passwordHasher.HashToken(request.Token), cancellationToken);
        if (token is null || token.UsedAt is not null || token.RevokedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ApiResponse<object>.Fail("Password reset token is invalid.");
        }

        if (!TryValidatePassword(request.NewPassword, out var passwordPolicyError))
        {
            return ApiResponse<object>.Fail(passwordPolicyError);
        }

        token.User.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        token.User.FailedLoginAttempts = 0;
        token.User.LockoutEndAt = null;
        token.UsedAt = DateTimeOffset.UtcNow;
        await authRepository.RevokeActivePasswordResetTokensAsync(token.UserId, cancellationToken);
        await authRepository.RevokeActiveRefreshTokensAsync(token.UserId, "PasswordReset", exceptSessionId: null, cancellationToken);
        await AddAuditLogAsync(token.User, "PasswordResetCompleted", null, cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { }, "Password reset successfully.");
    }

    public async Task<ApiResponse<object>> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResponse<object>.Fail("Current user is not available.");
        }

        var user = await authRepository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<object>.Fail("Current user is not available.");
        }

        if (!passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return ApiResponse<object>.Fail("Current password is invalid.");
        }

        if (!TryValidatePassword(request.NewPassword, out var passwordPolicyError))
        {
            return ApiResponse<object>.Fail(passwordPolicyError);
        }

        if (passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            return ApiResponse<object>.Fail("New password must be different from the current password.");
        }

        user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;

        await authRepository.RevokeActiveRefreshTokensAsync(user.Id, "PasswordChanged", exceptSessionId: null, cancellationToken);
        await authRepository.RevokeActivePasswordResetTokensAsync(user.Id, cancellationToken);
        await AddAuditLogAsync(user, "PasswordChanged", null, cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);
        return ApiResponse<object>.Ok(new { }, "Password changed successfully.");
    }

    public async Task<ApiResponse<EmailVerificationChallengeDto>> RequestEmailVerificationAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResponse<EmailVerificationChallengeDto>.Fail("Current user is not available.");
        }

        var user = await authRepository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<EmailVerificationChallengeDto>.Fail("Current user is not available.");
        }

        if (user.IsEmailVerified)
        {
            return ApiResponse<EmailVerificationChallengeDto>.Fail("Email is already verified.");
        }

        await authRepository.RevokeActiveEmailVerificationTokensAsync(user.Id, cancellationToken);

        var verificationToken = GenerateUrlSafeToken();
        var expiresAt = DateTimeOffset.UtcNow.Add(EmailVerificationLifetime);

        await authRepository.AddEmailVerificationTokenAsync(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = passwordHasher.HashToken(verificationToken),
            ExpiresAt = expiresAt
        }, cancellationToken);
        await identityEmailService.SendEmailVerificationAsync(user.Email, user.FullName, verificationToken, cancellationToken);
        await AddAuditLogAsync(user, "EmailVerificationRequested", null, cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<EmailVerificationChallengeDto>.Ok(new EmailVerificationChallengeDto
        {
            VerificationToken = ShouldExposeTokensForDevelopment() ? verificationToken : string.Empty,
            ExpiresAtUtc = expiresAt
        }, "Email verification token generated.");
    }

    public async Task<ApiResponse<object>> ConfirmEmailVerificationAsync(EmailVerificationRequest request, CancellationToken cancellationToken)
    {
        var token = await authRepository.GetEmailVerificationTokenAsync(passwordHasher.HashToken(request.Token), cancellationToken);
        if (token is null || token.UsedAt is not null || token.RevokedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ApiResponse<object>.Fail("Email verification token is invalid.");
        }

        token.User.IsEmailVerified = true;
        token.UsedAt = DateTimeOffset.UtcNow;
        await authRepository.RevokeActiveEmailVerificationTokensAsync(token.UserId, cancellationToken);
        await AddAuditLogAsync(token.User, "EmailVerified", null, cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { }, "Email verified successfully.");
    }

    public async Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResponse<CurrentUserDto>.Fail("Current user is not available.");
        }

        var user = await authRepository.GetUserByIdAsync(userId, cancellationToken);
        return user is null
            ? ApiResponse<CurrentUserDto>.Fail("Current user is not available.")
            : ApiResponse<CurrentUserDto>.Ok(MapCurrentUser(user));
    }

    public async Task<ApiResponse<CurrentUserDto>> UpdateCurrentUserAsync(ClaimsPrincipal principal, UpdateCurrentUserRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResponse<CurrentUserDto>.Fail("Current user is not available.");
        }

        var user = await authRepository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<CurrentUserDto>.Fail("Current user is not available.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return ApiResponse<CurrentUserDto>.Fail("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return ApiResponse<CurrentUserDto>.Fail("Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PreferredLanguage))
        {
            return ApiResponse<CurrentUserDto>.Fail("Preferred language is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TimeZone))
        {
            return ApiResponse<CurrentUserDto>.Fail("Time zone is required.");
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.PreferredLanguage = NormalizeLanguage(request.PreferredLanguage);
        user.TimeZone = request.TimeZone.Trim();
        user.EmailNotificationsEnabled = request.EmailNotificationsEnabled;
        user.SecurityNotificationsEnabled = request.SecurityNotificationsEnabled;
        user.ProductUpdatesEnabled = request.ProductUpdatesEnabled;

        await AddAuditLogAsync(user, "ProfileUpdated", new
        {
            user.PreferredLanguage,
            user.TimeZone,
            user.EmailNotificationsEnabled,
            user.SecurityNotificationsEnabled,
            user.ProductUpdatesEnabled
        }, cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);
        return ApiResponse<CurrentUserDto>.Ok(MapCurrentUser(user), "Profile updated successfully.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<AuthSessionDto>>> GetSessionsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResponse<IReadOnlyCollection<AuthSessionDto>>.Fail("Current user is not available.");
        }

        var currentSessionId = TryGetSessionId(principal, out var parsedSessionId) ? parsedSessionId : (Guid?)null;
        var sessions = await authRepository.ListActiveRefreshTokensAsync(userId, cancellationToken);

        var results = sessions
            .GroupBy(token => token.SessionId)
            .Select(group => group.OrderByDescending(token => token.LastUsedAt ?? token.CreatedAt).First())
            .OrderByDescending(token => token.LastUsedAt ?? token.CreatedAt)
            .Select(token => new AuthSessionDto
            {
                SessionId = token.SessionId,
                CreatedAtUtc = token.CreatedAt,
                LastUsedAtUtc = token.LastUsedAt,
                ExpiresAtUtc = token.ExpiresAt,
                IsCurrent = currentSessionId.HasValue && token.SessionId == currentSessionId.Value,
                IpAddress = token.CreatedByIpAddress,
                UserAgent = token.CreatedByUserAgent
            })
            .ToList();

        return ApiResponse<IReadOnlyCollection<AuthSessionDto>>.Ok(results);
    }

    public async Task<ApiResponse<object>> RevokeSessionAsync(ClaimsPrincipal principal, Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResponse<object>.Fail("Current user is not available.");
        }

        var token = await authRepository.GetActiveRefreshTokenBySessionAsync(userId, sessionId, cancellationToken);
        if (token is null)
        {
            return ApiResponse<object>.Fail("Session was not found.");
        }

        var user = await authRepository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<object>.Fail("Current user is not available.");
        }

        token.RevokedAt = DateTimeOffset.UtcNow;
        token.RevokedReason = "SessionRevoked";
        await AddAuditLogAsync(user, "SessionRevoked", new { sessionId }, cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);
        return ApiResponse<object>.Ok(new { sessionId }, "Session revoked.");
    }

    public async Task<ApiResponse<object>> RevokeAllSessionsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResponse<object>.Fail("Current user is not available.");
        }

        var user = await authRepository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<object>.Fail("Current user is not available.");
        }

        await authRepository.RevokeActiveRefreshTokensAsync(userId, "AllSessionsRevoked", exceptSessionId: null, cancellationToken);
        await AddAuditLogAsync(user, "AllSessionsRevoked", null, cancellationToken);
        await authRepository.SaveChangesAsync(cancellationToken);
        return ApiResponse<object>.Ok(new { }, "All sessions revoked.");
    }

    private RefreshToken CreateRefreshToken(Guid userId, string refreshToken, Guid sessionId)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            TokenHash = passwordHasher.HashToken(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(GetRefreshTokenDays()),
            CreatedByIpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            CreatedByUserAgent = NormalizeUserAgent(httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString())
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

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }

    private static CurrentUserDto MapCurrentUser(User user)
    {
        return new CurrentUserDto
        {
            Id = user.Id,
            CompanyId = user.CompanyId,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            PreferredLanguage = user.PreferredLanguage,
            TimeZone = user.TimeZone,
            IsEmailVerified = user.IsEmailVerified,
            EmailNotificationsEnabled = user.EmailNotificationsEnabled,
            SecurityNotificationsEnabled = user.SecurityNotificationsEnabled,
            ProductUpdatesEnabled = user.ProductUpdatesEnabled,
            OrganizationRole = user.OrganizationMemberships
                .Where(membership => membership.CompanyId == user.CompanyId && membership.Status == OrganizationMemberStatus.Active.ToStorageValue())
                .Select(membership => membership.Role)
                .FirstOrDefault(),
            Roles = GetRoles(user),
            Permissions = GetPermissions(user)
        };
    }

    private static bool TryGetSessionId(ClaimsPrincipal principal, out Guid sessionId)
    {
        return Guid.TryParse(principal.FindFirstValue("session_id"), out sessionId);
    }

    private static string NormalizeLanguage(string language)
    {
        return language.Trim().ToLowerInvariant();
    }

    private static string? NormalizeUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var trimmed = userAgent.Trim();
        return trimmed.Length <= 256 ? trimmed : trimmed[..256];
    }

    private static bool TryValidatePassword(string password, out string error)
    {
        if (password.Length < 12)
        {
            error = "Password must be at least 12 characters and include upper, lower, numeric, and special characters.";
            return false;
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            error = "Password must be at least 12 characters and include upper, lower, numeric, and special characters.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string GenerateUrlSafeToken()
    {
        return WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
    }

    private bool ShouldExposeTokensForDevelopment()
    {
        return string.Equals(configuration["Email:Provider"], "Development", StringComparison.OrdinalIgnoreCase);
    }

    private async Task AddAuditLogAsync(User user, string action, object? details, CancellationToken cancellationToken)
    {
        await authRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(User),
            EntityId = user.Id,
            Action = action,
            Details = JsonSerializer.Serialize(new
            {
                user.CompanyId,
                user.Email,
                details,
                SessionId = httpContextAccessor.HttpContext?.User.FindFirstValue("session_id"),
                CorrelationId = httpContextAccessor.HttpContext?.TraceIdentifier
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }
}
