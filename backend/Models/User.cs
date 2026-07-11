namespace StayFlow.Api.Models;

public sealed class User : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockoutEndAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = [];
    public ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<Conversation> AssignedConversations { get; set; } = [];
}
