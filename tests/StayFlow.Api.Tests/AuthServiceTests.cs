using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;
using StayFlow.Api.Services.Email;

namespace StayFlow.Api.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public void Pbkdf2PasswordHasher_VerifiesOnlyCorrectPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.HashPassword("correct horse battery staple");

        Assert.True(hasher.VerifyPassword("correct horse battery staple", hash));
        Assert.False(hasher.VerifyPassword("wrong password", hash));
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenAndStoresRefreshToken()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        var service = CreateService(repository, hasher);

        var response = await service.LoginAsync(new LoginRequest
        {
            Email = repository.User.Email,
            Password = "a very strong password"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.NotEmpty(response.Data.AccessToken);
        Assert.NotEmpty(response.Data.RefreshToken);
        Assert.Single(repository.RefreshTokens);
        Assert.Equal(0, repository.User.FailedLoginAttempts);
        Assert.Null(repository.User.LockoutEndAt);
    }

    [Fact]
    public async Task LoginAsync_WithRepeatedInvalidPasswords_LocksAccount()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        var service = CreateService(repository, hasher);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await service.LoginAsync(new LoginRequest
            {
                Email = repository.User.Email,
                Password = "incorrect password"
            }, CancellationToken.None);
        }

        Assert.Equal(5, repository.User.FailedLoginAttempts);
        Assert.True(repository.User.LockoutEndAt > DateTimeOffset.UtcNow);
        Assert.Equal(5, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task SwitchOrganizationAsync_WithActiveMembership_UpdatesActiveOrganizationAndReturnsNewTokens()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        var otherCompanyId = Guid.NewGuid();
        repository.User.OrganizationMemberships.Add(new OrganizationMember
        {
            CompanyId = otherCompanyId,
            UserId = repository.User.Id,
            Role = "Host",
            Status = OrganizationMemberStatus.Active.ToStorageValue()
        });
        var service = CreateService(repository, hasher);

        var response = await service.SwitchOrganizationAsync(CreatePrincipal(repository.User.Id), otherCompanyId, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.NotEmpty(response.Data.AccessToken);
        Assert.NotEmpty(response.Data.RefreshToken);
        Assert.Equal(otherCompanyId, repository.User.CompanyId);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateCurrentUserAsync_WithValidRequest_UpdatesProfile()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        repository.User.OrganizationMemberships.Add(new OrganizationMember
        {
            CompanyId = repository.User.CompanyId,
            UserId = repository.User.Id,
            Role = "Administrator",
            Status = OrganizationMemberStatus.Active.ToStorageValue()
        });
        var service = CreateService(repository, hasher);

        var response = await service.UpdateCurrentUserAsync(CreatePrincipal(repository.User.Id), new UpdateCurrentUserRequest
        {
            FullName = "Updated User",
            PhoneNumber = "+254700000999",
            PreferredLanguage = "fr",
            TimeZone = "Africa/Nairobi",
            EmailNotificationsEnabled = false,
            SecurityNotificationsEnabled = true,
            ProductUpdatesEnabled = true
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Updated User", repository.User.FullName);
        Assert.Equal("+254700000999", repository.User.PhoneNumber);
        Assert.Equal("fr", repository.User.PreferredLanguage);
        Assert.Equal("Africa/Nairobi", repository.User.TimeZone);
        Assert.Equal("Administrator", response.Data?.OrganizationRole);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_Fails()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        var service = CreateService(repository, hasher);

        var response = await service.ChangePasswordAsync(CreatePrincipal(repository.User.Id), new ChangePasswordRequest
        {
            CurrentPassword = "wrong password",
            NewPassword = "An even stronger password 123!"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Current password is invalid.", response.Message);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithValidRequest_UpdatesPasswordAndClearsLockout()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        repository.User.FailedLoginAttempts = 3;
        repository.User.LockoutEndAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var service = CreateService(repository, hasher);

        var response = await service.ChangePasswordAsync(CreatePrincipal(repository.User.Id), new ChangePasswordRequest
        {
            CurrentPassword = "a very strong password",
            NewPassword = "An even stronger password 123!"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(hasher.VerifyPassword("An even stronger password 123!", repository.User.PasswordHash));
        Assert.Equal(0, repository.User.FailedLoginAttempts);
        Assert.Null(repository.User.LockoutEndAt);
        Assert.All(repository.RefreshTokens, token => Assert.NotNull(token.RevokedAt));
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task RequestEmailVerificationAsync_ForUnverifiedUser_CreatesVerificationToken()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        repository.User.IsEmailVerified = false;
        var service = CreateService(repository, hasher);

        var response = await service.RequestEmailVerificationAsync(CreatePrincipal(repository.User.Id), CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.NotEmpty(response.Data.VerificationToken);
        Assert.Single(repository.EmailVerificationTokens);
        Assert.Single(repository.EmailMessages);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task RequestEmailVerificationAsync_ForVerifiedUser_Fails()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        var service = CreateService(repository, hasher);

        var response = await service.RequestEmailVerificationAsync(CreatePrincipal(repository.User.Id), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Email is already verified.", response.Message);
        Assert.Empty(repository.EmailVerificationTokens);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_DoesNotRevealMissingAccount()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        var service = CreateService(repository, hasher);

        var response = await service.RequestPasswordResetAsync(new PasswordResetRequest
        {
            Email = "missing@example.com"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("If the account exists, a password reset token has been generated.", response.Message);
        Assert.Empty(repository.PasswordResetTokens);
        Assert.Empty(repository.EmailMessages);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_RevokesPriorUnusedTokens()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        repository.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = repository.User.Id,
            TokenHash = hasher.HashToken("old-token"),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        var service = CreateService(repository, hasher);

        var response = await service.RequestPasswordResetAsync(new PasswordResetRequest
        {
            Email = repository.User.Email
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(2, repository.PasswordResetTokens.Count);
        Assert.NotNull(repository.PasswordResetTokens[0].RevokedAt);
        Assert.Single(repository.EmailMessages);
    }

    [Fact]
    public async Task GetSessionsAsync_ReturnsActiveSessions()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        repository.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = repository.User.Id,
            User = repository.User,
            SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TokenHash = hasher.HashToken("token-a"),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        var service = CreateService(repository, hasher);

        var response = await service.GetSessionsAsync(CreatePrincipal(repository.User.Id, "11111111-1111-1111-1111-111111111111"), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(response.Data!);
        Assert.True(response.Data!.Single().IsCurrent);
    }

    [Fact]
    public async Task RevokeSessionAsync_RevokesOnlyTargetSession()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new FakeAuthRepository();
        repository.User = NewUser(hasher.HashPassword("a very strong password"));
        var targetSession = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var otherSession = Guid.Parse("22222222-2222-2222-2222-222222222222");
        repository.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = repository.User.Id,
            User = repository.User,
            SessionId = targetSession,
            TokenHash = hasher.HashToken("token-a"),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        repository.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = repository.User.Id,
            User = repository.User,
            SessionId = otherSession,
            TokenHash = hasher.HashToken("token-b"),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        var service = CreateService(repository, hasher);

        var response = await service.RevokeSessionAsync(CreatePrincipal(repository.User.Id), targetSession, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(repository.RefreshTokens.Single(token => token.SessionId == targetSession).RevokedAt);
        Assert.Null(repository.RefreshTokens.Single(token => token.SessionId == otherSession).RevokedAt);
    }

    private static AuthService CreateService(FakeAuthRepository repository, IPasswordHasher hasher)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "StayFlow.Api.Tests",
                ["Jwt:Audience"] = "StayFlow.Tests",
                ["Jwt:SigningKey"] = "test-secret-key-with-at-least-32-characters",
                ["Jwt:AccessTokenMinutes"] = "30",
                ["Jwt:RefreshTokenDays"] = "30",
                ["Email:Provider"] = "Development"
            })
            .Build();

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        httpContextAccessor.HttpContext.Request.Headers.UserAgent = "StayFlow.Api.Tests";

        var dbContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"auth-service-tests-{Guid.NewGuid():N}")
                .Options);

        return new AuthService(
            repository,
            new JwtTokenService(configuration, hasher),
            hasher,
            configuration,
            httpContextAccessor,
            new FakeIdentityEmailService(repository),
            dbContext,
            new NoOpSubscriptionEntitlementService(),
            new TenantExecutionContextAccessor());
    }

    private static User NewUser(string passwordHash)
    {
        var companyId = Guid.NewGuid();
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            RolePermissions =
            [
                new RolePermission
                {
                    Permission = new Permission
                    {
                        Id = Guid.NewGuid(),
                        Name = "companies.manage"
                    }
                }
            ]
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FullName = "Test User",
            Email = "test@example.com",
            PhoneNumber = "+254700000002",
            PreferredLanguage = "en",
            TimeZone = "UTC",
            Role = "Admin",
            PasswordHash = passwordHash,
            IsActive = true,
            IsEmailVerified = true,
            EmailNotificationsEnabled = true,
            SecurityNotificationsEnabled = true,
            ProductUpdatesEnabled = false
        };

        user.UserRoles.Add(new UserRole
        {
            User = user,
            UserId = user.Id,
            Role = role,
            RoleId = role.Id
        });

        return user;
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string? sessionId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            claims.Add(new Claim("session_id", sessionId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
        claims,
        authenticationType: "Test"));
    }

    private sealed class FakeAuthRepository : IAuthRepository
    {
        public User User { get; set; } = null!;
        public List<RefreshToken> RefreshTokens { get; } = [];
        public List<PasswordResetToken> PasswordResetTokens { get; } = [];
        public List<EmailVerificationToken> EmailVerificationTokens { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];
        public List<EmailMessage> EmailMessages { get; } = [];
        public int SaveChangesCallCount { get; private set; }

        public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(User.Email == email && User.IsActive ? User : null);
        }

        public Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(User.Id == id && User.IsActive ? User : null);
        }

        public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
        {
            return Task.FromResult(RefreshTokens.FirstOrDefault(token => token.TokenHash == tokenHash));
        }

        public Task<IReadOnlyCollection<RefreshToken>> ListActiveRefreshTokensAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<RefreshToken>>(RefreshTokens
                .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > DateTimeOffset.UtcNow)
                .ToList());
        }

        public Task<RefreshToken?> GetActiveRefreshTokenBySessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(RefreshTokens.FirstOrDefault(token => token.UserId == userId && token.SessionId == sessionId && token.RevokedAt == null && token.ExpiresAt > DateTimeOffset.UtcNow));
        }

        public Task<PasswordResetToken?> GetPasswordResetTokenAsync(string tokenHash, CancellationToken cancellationToken)
        {
            return Task.FromResult(PasswordResetTokens.FirstOrDefault(token => token.TokenHash == tokenHash));
        }

        public Task<EmailVerificationToken?> GetEmailVerificationTokenAsync(string tokenHash, CancellationToken cancellationToken)
        {
            return Task.FromResult(EmailVerificationTokens.FirstOrDefault(token => token.TokenHash == tokenHash));
        }

        public Task RevokeActiveRefreshTokensAsync(Guid userId, string reason, Guid? exceptSessionId, CancellationToken cancellationToken)
        {
            foreach (var token in RefreshTokens.Where(token => token.UserId == userId && token.RevokedAt == null && (!exceptSessionId.HasValue || token.SessionId != exceptSessionId.Value)))
            {
                token.RevokedAt = DateTimeOffset.UtcNow;
                token.RevokedReason = reason;
            }

            return Task.CompletedTask;
        }

        public Task RevokeActivePasswordResetTokensAsync(Guid userId, CancellationToken cancellationToken)
        {
            foreach (var token in PasswordResetTokens.Where(token => token.UserId == userId && token.UsedAt == null && token.RevokedAt == null && token.ExpiresAt > DateTimeOffset.UtcNow))
            {
                token.RevokedAt = DateTimeOffset.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task RevokeActiveEmailVerificationTokensAsync(Guid userId, CancellationToken cancellationToken)
        {
            foreach (var token in EmailVerificationTokens.Where(token => token.UserId == userId && token.UsedAt == null && token.RevokedAt == null && token.ExpiresAt > DateTimeOffset.UtcNow))
            {
                token.RevokedAt = DateTimeOffset.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            refreshToken.User = User;
            RefreshTokens.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task AddPasswordResetTokenAsync(PasswordResetToken passwordResetToken, CancellationToken cancellationToken)
        {
            passwordResetToken.User = User;
            PasswordResetTokens.Add(passwordResetToken);
            return Task.CompletedTask;
        }

        public Task AddEmailVerificationTokenAsync(EmailVerificationToken emailVerificationToken, CancellationToken cancellationToken)
        {
            emailVerificationToken.User = User;
            EmailVerificationTokens.Add(emailVerificationToken);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIdentityEmailService(FakeAuthRepository repository) : IIdentityEmailService
    {
        public Task SendPasswordResetAsync(string email, string fullName, string token, CancellationToken cancellationToken)
        {
            repository.EmailMessages.Add(new EmailMessage { ToAddress = email, Subject = "Password reset", PlainTextBody = token });
            return Task.CompletedTask;
        }

        public Task SendEmailVerificationAsync(string email, string fullName, string token, CancellationToken cancellationToken)
        {
            repository.EmailMessages.Add(new EmailMessage { ToAddress = email, Subject = "Email verification", PlainTextBody = token });
            return Task.CompletedTask;
        }

        public Task SendOrganizationInvitationAsync(string email, string role, string token, CancellationToken cancellationToken)
        {
            repository.EmailMessages.Add(new EmailMessage { ToAddress = email, Subject = "Invitation", PlainTextBody = token });
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpSubscriptionEntitlementService : ISubscriptionEntitlementService
    {
        public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken)
            => Task.FromResult(new SubscriptionSnapshot(
                companyId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Free",
                "Free",
                "Active",
                false,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(29),
                [],
                []));

        public Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid companyId, CancellationToken cancellationToken)
            => Task.FromResult<SubscriptionSnapshot?>(null);

        public Task EnsureFeatureEnabledAsync(Guid companyId, string featureKey, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<UsageConsumptionResult> ConsumeQuotaAsync(Guid companyId, UsageMetric metric, long quantity, string idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult(new UsageConsumptionResult(metric, null, 0, quantity, true, false));

        public Task<SubscriptionSnapshot> UpdatePlanAsync(Guid companyId, Guid? planId, string? planName, string? notes, CancellationToken cancellationToken)
            => GetCurrentSnapshotAsync(companyId, cancellationToken);
    }
}
