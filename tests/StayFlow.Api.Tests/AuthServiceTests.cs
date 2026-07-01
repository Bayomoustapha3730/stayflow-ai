using Microsoft.Extensions.Configuration;
using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

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

    private static AuthService CreateService(FakeAuthRepository repository, IPasswordHasher hasher)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "StayFlow.Api.Tests",
                ["Jwt:Audience"] = "StayFlow.Tests",
                ["Jwt:SigningKey"] = "test-secret-key-with-at-least-32-characters",
                ["Jwt:AccessTokenMinutes"] = "30",
                ["Jwt:RefreshTokenDays"] = "30"
            })
            .Build();

        return new AuthService(repository, new JwtTokenService(configuration, hasher), hasher, configuration);
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
            Role = "Admin",
            PasswordHash = passwordHash,
            IsActive = true,
            IsEmailVerified = true
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

    private sealed class FakeAuthRepository : IAuthRepository
    {
        public User User { get; set; } = null!;
        public List<RefreshToken> RefreshTokens { get; } = [];
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

        public Task<PasswordResetToken?> GetPasswordResetTokenAsync(string tokenHash, CancellationToken cancellationToken)
        {
            return Task.FromResult<PasswordResetToken?>(null);
        }

        public Task<EmailVerificationToken?> GetEmailVerificationTokenAsync(string tokenHash, CancellationToken cancellationToken)
        {
            return Task.FromResult<EmailVerificationToken?>(null);
        }

        public Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            refreshToken.User = User;
            RefreshTokens.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task AddPasswordResetTokenAsync(PasswordResetToken passwordResetToken, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AddEmailVerificationTokenAsync(EmailVerificationToken emailVerificationToken, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }
}
