using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class AuthRepository(ApplicationDbContext dbContext) : IAuthRepository
{
    public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return UsersWithAuthorization()
            .FirstOrDefaultAsync(user => user.Email == email && user.IsActive, cancellationToken);
    }

    public Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return UsersWithAuthorization()
            .FirstOrDefaultAsync(user => user.Id == id && user.IsActive, cancellationToken);
    }

    public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return dbContext.RefreshTokens
            .Include(token => token.User)
            .ThenInclude(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .ThenInclude(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .Include(token => token.User)
            .ThenInclude(user => user.OrganizationMemberships)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyCollection<RefreshToken>> ListActiveRefreshTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId
                && token.RevokedAt == null
                && token.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(token => token.LastUsedAt ?? token.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<RefreshToken?> GetActiveRefreshTokenBySessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        return dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.UserId == userId
                && token.SessionId == sessionId
                && token.RevokedAt == null
                && token.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
    }

    public Task<PasswordResetToken?> GetPasswordResetTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return dbContext.PasswordResetTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public Task<EmailVerificationToken?> GetEmailVerificationTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return dbContext.EmailVerificationTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task RevokeActiveRefreshTokensAsync(Guid userId, string reason, Guid? exceptSessionId, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId
                && token.RevokedAt == null
                && token.ExpiresAt > DateTimeOffset.UtcNow
                && (!exceptSessionId.HasValue || token.SessionId != exceptSessionId.Value))
            .ToListAsync(cancellationToken);

        var revokedAt = DateTimeOffset.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = revokedAt;
            token.RevokedReason = reason;
        }
    }

    public async Task RevokeActivePasswordResetTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.PasswordResetTokens
            .Where(token => token.UserId == userId
                && token.UsedAt == null
                && token.RevokedAt == null
                && token.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        var revokedAt = DateTimeOffset.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = revokedAt;
        }
    }

    public async Task RevokeActiveEmailVerificationTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.EmailVerificationTokens
            .Where(token => token.UserId == userId
                && token.UsedAt == null
                && token.RevokedAt == null
                && token.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        var revokedAt = DateTimeOffset.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = revokedAt;
        }
    }

    public async Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
    }

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
    }

    public async Task AddPasswordResetTokenAsync(PasswordResetToken passwordResetToken, CancellationToken cancellationToken)
    {
        await dbContext.PasswordResetTokens.AddAsync(passwordResetToken, cancellationToken);
    }

    public async Task AddEmailVerificationTokenAsync(EmailVerificationToken emailVerificationToken, CancellationToken cancellationToken)
    {
        await dbContext.EmailVerificationTokens.AddAsync(emailVerificationToken, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<User> UsersWithAuthorization()
    {
        return dbContext.Users
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .ThenInclude(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .Include(user => user.OrganizationMemberships);
    }
}
