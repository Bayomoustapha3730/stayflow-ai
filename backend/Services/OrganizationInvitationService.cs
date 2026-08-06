using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Organizations;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class OrganizationInvitationService(
    ApplicationDbContext dbContext,
    ICurrentTenantContext tenantContext,
    IPasswordHasher passwordHasher,
    IConfiguration configuration) : IOrganizationInvitationService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(7);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    public async Task<ApiResponse<CreatedOrganizationInvitationDto>> CreateAsync(CreateOrganizationInvitationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<CreatedOrganizationInvitationDto>.Fail(error);
        }

        if (!TryValidateRole(request.Role, out var normalizedRole, out error))
        {
            return ApiResponse<CreatedOrganizationInvitationDto>.Fail(error);
        }

        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return ApiResponse<CreatedOrganizationInvitationDto>.Fail("A valid invitation email is required.");
        }

        var normalizedEmail = email.ToUpperInvariant();
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
        await AddAuditLogAsync(companyId, invitation.Id, "InvitationCreated", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<CreatedOrganizationInvitationDto>.Ok(new CreatedOrganizationInvitationDto
        {
            Invitation = Map(invitation),
            InvitationToken = plainToken,
            InvitationLink = BuildInvitationLink(plainToken)
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

        await AddAuditLogAsync(companyId, invitation.Id, "InvitationResent", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<ResentOrganizationInvitationDto>.Ok(new ResentOrganizationInvitationDto
        {
            Invitation = Map(invitation),
            InvitationToken = plainToken,
            InvitationLink = BuildInvitationLink(plainToken)
        }, "Invitation resent.");
    }

    public async Task<ApiResponse<object>> AcceptAsync(AcceptOrganizationInvitationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<object>.Fail(error);
        }

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

        if (invitation.CompanyId != companyId)
        {
            return ApiResponse<object>.Fail("Invitation does not belong to the current tenant.");
        }

        if (invitation.RevokedAtUtc is not null)
        {
            return ApiResponse<object>.Fail("Invitation has been revoked.");
        }

        if (invitation.AcceptedAtUtc is not null)
        {
            return ApiResponse<object>.Fail("Invitation has already been used.");
        }

        if (invitation.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return ApiResponse<object>.Fail("Invitation has expired.");
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId && item.CompanyId == companyId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<object>.Fail("Current user was not found.");
        }

        var normalizedEmail = user.Email.Trim().ToUpperInvariant();
        if (!string.Equals(normalizedEmail, invitation.NormalizedEmail, StringComparison.Ordinal))
        {
            return ApiResponse<object>.Fail("Invitation email does not match the signed in user.");
        }

        var existingMembership = await dbContext.OrganizationMembers
            .FirstOrDefaultAsync(item => item.CompanyId == companyId
                && item.UserId == userId
                && item.Status == OrganizationMemberStatus.Active.ToStorageValue(), cancellationToken);

        if (existingMembership is null)
        {
            await dbContext.OrganizationMembers.AddAsync(new OrganizationMember
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
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
            existingMembership.Status = OrganizationMemberStatus.Active.ToStorageValue();
        }

        invitation.AcceptedAtUtc = DateTimeOffset.UtcNow;
        invitation.AcceptedByUserId = userId;

        await AddAuditLogAsync(companyId, invitation.Id, "InvitationAccepted", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<object>.Ok(new { invitationId = invitation.Id }, "Invitation accepted.");
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

    private static bool TryValidateRole(string role, out string normalizedRole, out string error)
    {
        normalizedRole = string.Empty;
        error = string.Empty;

        if (!OrganizationRoleExtensions.TryParse(role, out var parsed))
        {
            error = "Invitation role is invalid.";
            return false;
        }

        if (parsed == OrganizationRole.Owner)
        {
            error = "Owner role cannot be granted through invitations.";
            return false;
        }

        normalizedRole = parsed.ToStorageValue();
        return true;
    }

    private string HashInvitationToken(string token)
    {
        var pepper = configuration["Jwt:SigningKey"] ?? "stayflow-invitation-pepper";
        var combined = $"{pepper}:{token}";
        return passwordHasher.HashToken(combined);
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
}