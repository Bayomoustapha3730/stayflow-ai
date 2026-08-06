using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IAuthRepository
{
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RefreshToken>> ListActiveRefreshTokensAsync(Guid userId, CancellationToken cancellationToken);
    Task<RefreshToken?> GetActiveRefreshTokenBySessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task<PasswordResetToken?> GetPasswordResetTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task<EmailVerificationToken?> GetEmailVerificationTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task RevokeActiveRefreshTokensAsync(Guid userId, string reason, Guid? exceptSessionId, CancellationToken cancellationToken);
    Task RevokeActivePasswordResetTokensAsync(Guid userId, CancellationToken cancellationToken);
    Task RevokeActiveEmailVerificationTokensAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken);
    Task AddPasswordResetTokenAsync(PasswordResetToken passwordResetToken, CancellationToken cancellationToken);
    Task AddEmailVerificationTokenAsync(EmailVerificationToken emailVerificationToken, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
