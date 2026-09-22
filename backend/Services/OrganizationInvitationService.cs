using System.Data;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Auth;
using StayFlow.Api.DTOs.Organizations;
using StayFlow.Api.Models;
using StayFlow.Api.Services.Email;

namespace StayFlow.Api.Services;

public sealed class OrganizationInvitationService(
    ApplicationDbContext dbContext,
    ICurrentTenantContext tenantContext,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    IIdentityEmailService identityEmailService,
    IJwtTokenService jwtTokenService,
    IHttpContextAccessor httpContextAccessor,
    ITenantExecutionContextAccessor tenantExecutionContextAccessor,
    IResourceCapacityService resourceCapacityService) : IOrganizationInvitationService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(7);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    public async Task<ApiResponse<CreatedOrganizationInvitationDto>> CreateAsync(CreateOrganizationInvitationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<CreatedOrganizationInvitationDto>.Fail(error);
        }

        if (!OrganizationInvitationValidation.TryValidateRole(request.Role, out var normalizedRole, out error))
        {
            return ApiResponse<CreatedOrganizationInvitationDto>.Fail(error);
        }

        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return ApiResponse<CreatedOrganizationInvitationDto>.Fail("A valid invitation email is required.");
        }

        var normalizedEmail = EmailIdentityNormalizer.Normalize(email);
        var existing = await dbContext.OrganizationInvitations
            .AsNoTracking()
            .AnyAsync(item => item.CompanyId == companyId
                && item.NormalizedEmail == normalizedEmail
                && item.AcceptedAtUtc == null
                && item.RevokedAtUtc == null
                && item.ExpiresAtUtc > DateTimeOffset.UtcNow, cancellationToken);
        if (existing)
        {
            return ApiResponse<CreatedOrganizationInvitationDto>.Fail("An active invitation already exists for this email.");
        }

        var plainToken = GenerateToken();
        var expiresAt = DateTimeOffset.UtcNow.Add(request.ExpiresInHours is > 0 and <= 24 * 30
            ? TimeSpan.FromHours(request.ExpiresInHours.Value)
            : DefaultExpiry);

        var invitation = new OrganizationInvitation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            InvitedByUserId = userId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = normalizedRole,
            TokenHash = HashInvitationToken(plainToken),
            ExpiresAtUtc = expiresAt,
            LastSentAtUtc = DateTimeOffset.UtcNow,
            SendCount = 1
        };

        await dbContext.OrganizationInvitations.AddAsync(invitation, cancellationToken);
        await identityEmailService.SendOrganizationInvitationAsync(invitation.Email, invitation.Role, plainToken, cancellationToken);
        await AddAuditLogAsync(companyId, invitation.Id, "InvitationCreated", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<CreatedOrganizationInvitationDto>.Ok(new CreatedOrganizationInvitationDto
        {
            Invitation = Map(invitation),
            InvitationToken = ShouldExposeTokensForDevelopment() ? plainToken : string.Empty,
            InvitationLink = ShouldExposeTokensForDevelopment() ? BuildInvitationLink(plainToken) : string.Empty
        }, "Invitation created.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<OrganizationInvitationDto>>> ListAsync(CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out _, out var error))
        {
            return ApiResponse<IReadOnlyCollection<OrganizationInvitationDto>>.Fail(error);
        }

        var invitations = await dbContext.OrganizationInvitations
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => Map(item))
            .ToListAsync(cancellationToken);

        return ApiResponse<IReadOnlyCollection<OrganizationInvitationDto>>.Ok(invitations);
    }

    public async Task<ApiResponse<object>> RevokeAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out _, out var error))
        {
            return ApiResponse<object>.Fail(error);
        }

        var invitation = await dbContext.OrganizationInvitations
            .FirstOrDefaultAsync(item => item.Id == invitationId && item.CompanyId == companyId, cancellationToken);
        if (invitation is null)
        {
            return ApiResponse<object>.Fail("Invitation was not found.");
        }

        if (invitation.AcceptedAtUtc is not null)
        {
            return ApiResponse<object>.Fail("Accepted invitations cannot be revoked.");
        }

        invitation.RevokedAtUtc = DateTimeOffset.UtcNow;
        await AddAuditLogAsync(companyId, invitation.Id, "InvitationRevoked", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<object>.Ok(new { invitationId }, "Invitation revoked.");
    }

    public async Task<ApiResponse<ResentOrganizationInvitationDto>> ResendAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out _, out var error))
        {
            return ApiResponse<ResentOrganizationInvitationDto>.Fail(error);
        }

        var invitation = await dbContext.OrganizationInvitations
            .FirstOrDefaultAsync(item => item.Id == invitationId && item.CompanyId == companyId, cancellationToken);
        if (invitation is null)
        {
            return ApiResponse<ResentOrganizationInvitationDto>.Fail("Invitation was not found.");
        }

        if (invitation.RevokedAtUtc is not null)
        {
            return ApiResponse<ResentOrganizationInvitationDto>.Fail("Revoked invitations cannot be resent.");
        }

        if (invitation.AcceptedAtUtc is not null)
        {
            return ApiResponse<ResentOrganizationInvitationDto>.Fail("Accepted invitations cannot be resent.");
        }

        var now = DateTimeOffset.UtcNow;
        if (invitation.LastSentAtUtc.HasValue && now - invitation.LastSentAtUtc.Value < ResendCooldown)
        {
            return ApiResponse<ResentOrganizationInvitationDto>.Fail("Invitation resend is rate limited. Try again shortly.");
        }

        var plainToken = GenerateToken();
        invitation.TokenHash = HashInvitationToken(plainToken);
        invitation.LastSentAtUtc = now;
        invitation.SendCount++;
        invitation.ExpiresAtUtc = now.Add(DefaultExpiry);

        await identityEmailService.SendOrganizationInvitationAsync(invitation.Email, invitation.Role, plainToken, cancellationToken);
        await AddAuditLogAsync(companyId, invitation.Id, "InvitationResent", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<ResentOrganizationInvitationDto>.Ok(new ResentOrganizationInvitationDto
        {
            Invitation = Map(invitation),
            InvitationToken = ShouldExposeTokensForDevelopment() ? plainToken : string.Empty,
            InvitationLink = ShouldExposeTokensForDevelopment() ? BuildInvitationLink(plainToken) : string.Empty
        }, "Invitation resent.");
    }

    public async Task<ApiResponse<AuthTokenResponse>> AcceptAsync(AcceptOrganizationInvitationRequest request, CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId ?? Guid.Empty;
        if (!tenantContext.IsAuthenticated || userId == Guid.Empty)
        {
            return ApiResponse<AuthTokenResponse>.Fail("Authenticated user context is required.");
        }

        var plainToken = request.Token.Trim();
        if (string.IsNullOrWhiteSpace(plainToken))
        {
            return ApiResponse<AuthTokenResponse>.Fail("Invitation token is required.");
        }

        var tokenHash = HashInvitationToken(plainToken);
        IDbContextTransaction? transaction = null;

        try
        {
            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            }

            OrganizationInvitation? invitation;
                if (transaction is not null && string.Equals(
                    dbContext.Database.ProviderName,
                    "Npgsql.EntityFrameworkCore.PostgreSQL",
                    StringComparison.Ordinal))
            {
                invitation = await dbContext.OrganizationInvitations
                    .FromSqlInterpolated($@"SELECT * FROM ""OrganizationInvitations"" WHERE ""TokenHash"" = {tokenHash} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
            }
            else
            {
                invitation = await dbContext.OrganizationInvitations
                    .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
            }

            if (invitation is null)
            {
                return ApiResponse<AuthTokenResponse>.Fail("Invitation is invalid.");
            }

            if (!OrganizationInvitationValidation.TryValidate(invitation, out var validationError))
            {
                return ApiResponse<AuthTokenResponse>.Fail(validationError);
            }

            var user = await dbContext.Users
                .Include(item => item.UserRoles)
                    .ThenInclude(item => item.Role)
                        .ThenInclude(item => item.RolePermissions)
                            .ThenInclude(item => item.Permission)
                .FirstOrDefaultAsync(item => item.Id == userId && item.IsActive, cancellationToken);
            if (user is null)
            {
                return ApiResponse<AuthTokenResponse>.Fail("Current user was not found.");
            }

            var normalizedEmail = EmailIdentityNormalizer.Normalize(user.Email);
            if (!string.Equals(normalizedEmail, invitation.NormalizedEmail, StringComparison.Ordinal))
            {
                return ApiResponse<AuthTokenResponse>.Fail("Invitation email does not match the signed in user.");
            }

            var existingMembership = await dbContext.OrganizationMembers
                .FirstOrDefaultAsync(item => item.CompanyId == invitation.CompanyId
                    && item.UserId == userId
                    && item.Status == OrganizationMemberStatus.Active.ToStorageValue(), cancellationToken);

            if (existingMembership is null)
            {
                await resourceCapacityService.EnsureCapacityAsync(invitation.CompanyId, UsageMetric.Users, cancellationToken);
                await dbContext.OrganizationMembers.AddAsync(new OrganizationMember
                {
                    Id = Guid.NewGuid(),
                    CompanyId = invitation.CompanyId,
                    UserId = userId,
                    Role = invitation.Role,
                    Status = OrganizationMemberStatus.Active.ToStorageValue(),
                    JoinedAt = DateTimeOffset.UtcNow,
                    InvitedByUserId = invitation.InvitedByUserId
                }, cancellationToken);
            }
            else
            {
                existingMembership.Role = invitation.Role;
                if (existingMembership.Status != OrganizationMemberStatus.Active.ToStorageValue())
                {
                    await resourceCapacityService.EnsureCapacityAsync(invitation.CompanyId, UsageMetric.Users, cancellationToken);
                    existingMembership.Status = OrganizationMemberStatus.Active.ToStorageValue();
                }
            }

            invitation.AcceptedAtUtc = DateTimeOffset.UtcNow;
            invitation.AcceptedByUserId = userId;
            user.CompanyId = invitation.CompanyId;

            var activeRefreshTokens = await dbContext.RefreshTokens
                .Where(token => token.UserId == userId
                    && token.RevokedAt == null
                    && token.ExpiresAt > DateTimeOffset.UtcNow)
                .ToListAsync(cancellationToken);

            foreach (var activeRefreshToken in activeRefreshTokens)
            {
                activeRefreshToken.RevokedAt = DateTimeOffset.UtcNow;
                activeRefreshToken.RevokedReason = "InvitationAccepted";
            }

            var sessionId = Guid.NewGuid();
            var response = jwtTokenService.CreateTokenResponse(user, GetRoles(user), GetPermissions(user), sessionId);
            await dbContext.RefreshTokens.AddAsync(CreateRefreshToken(user.Id, response.RefreshToken, sessionId), cancellationToken);
            var previousCompanyId = tenantExecutionContextAccessor.CompanyId;
            var previousUserId = tenantExecutionContextAccessor.UserId;
            var previousCorrelationId = tenantExecutionContextAccessor.CorrelationId;
            var hadTenantExecutionContext = tenantExecutionContextAccessor.IsAuthenticated;
            try
            {
                tenantExecutionContextAccessor.Set(invitation.CompanyId, userId, previousCorrelationId);
                await AddAuditLogAsync(invitation.CompanyId, invitation.Id, "InvitationAccepted", cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                if (hadTenantExecutionContext && previousCompanyId is { } restoredCompanyId && restoredCompanyId != Guid.Empty)
                {
                    tenantExecutionContextAccessor.Set(restoredCompanyId, previousUserId, previousCorrelationId);
                }
                else
                {
                    tenantExecutionContextAccessor.Clear();
                }
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return ApiResponse<AuthTokenResponse>.Ok(response, "Invitation accepted.");
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<ApiResponse<object>> RejectAsync(RejectOrganizationInvitationRequest request, CancellationToken cancellationToken)
    {
        var plainToken = request.Token.Trim();
        if (string.IsNullOrWhiteSpace(plainToken))
        {
            return ApiResponse<object>.Fail("Invitation token is required.");
        }

        var tokenHash = HashInvitationToken(plainToken);
        var invitation = await dbContext.OrganizationInvitations
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        if (invitation is null)
        {
            return ApiResponse<object>.Fail("Invitation is invalid.");
        }

        if (invitation.AcceptedAtUtc is not null)
        {
            return ApiResponse<object>.Fail("Invitation has already been used.");
        }

        if (invitation.RejectedAtUtc is not null)
        {
            return ApiResponse<object>.Fail("Invitation has already been rejected.");
        }

        if (invitation.RevokedAtUtc is not null)
        {
            return ApiResponse<object>.Fail("Invitation has been revoked.");
        }

        if (invitation.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return ApiResponse<object>.Fail("Invitation has expired.");
        }

        invitation.RejectedAtUtc = DateTimeOffset.UtcNow;
        await AddAuditLogAsync(invitation.CompanyId, invitation.Id, "InvitationRejected", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<object>.Ok(new { invitationId = invitation.Id }, "Invitation rejected.");
    }

    private bool TryGetContext(out Guid companyId, out Guid userId, out string error)
    {
        companyId = tenantContext.CompanyId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;

        if (!tenantContext.IsAuthenticated || companyId == Guid.Empty || userId == Guid.Empty)
        {
            error = "Authenticated tenant context is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private string HashInvitationToken(string token)
    {
        return OrganizationInvitationValidation.HashToken(token, passwordHasher, configuration);
    }

    private static string GenerateToken()
    {
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
    }

    private static OrganizationInvitationDto Map(OrganizationInvitation invitation)
    {
        return new OrganizationInvitationDto
        {
            Id = invitation.Id,
            Email = invitation.Email,
            Role = invitation.Role,
            ExpiresAtUtc = invitation.ExpiresAtUtc,
            AcceptedAtUtc = invitation.AcceptedAtUtc,
            RejectedAtUtc = invitation.RejectedAtUtc,
            RevokedAtUtc = invitation.RevokedAtUtc,
            LastSentAtUtc = invitation.LastSentAtUtc,
            SendCount = invitation.SendCount
        };
    }

    private string BuildInvitationLink(string token)
    {
        var frontendBase = configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "https://example.invalid";
        return $"{frontendBase}/onboarding/team?token={Uri.EscapeDataString(token)}";
    }

    private async Task AddAuditLogAsync(Guid companyId, Guid invitationId, string action, CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(OrganizationInvitation),
            EntityId = invitationId,
            Action = action,
            Details = $"{{\"companyId\":\"{companyId}\",\"invitedBy\":\"{tenantContext.UserId}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private bool ShouldExposeTokensForDevelopment()
    {
        return string.Equals(configuration["Email:Provider"], "Development", StringComparison.OrdinalIgnoreCase);
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

    private static string? NormalizeUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var trimmed = userAgent.Trim();
        return trimmed.Length <= 256 ? trimmed : trimmed[..256];
    }
}