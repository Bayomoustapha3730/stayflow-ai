using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Organizations;
using StayFlow.Api.Models;
using StayFlow.Api.Data;

namespace StayFlow.Api.Services;

public sealed class OrganizationService(
    ApplicationDbContext dbContext,
    ITenantContext tenantContext) : IOrganizationService
{
    public async Task<ApiResponse<OrganizationDto>> GetCurrentAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenantCompanyId(out var companyId, out var error))
        {
            return ApiResponse<OrganizationDto>.Fail(error);
        }

        var company = await dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);

        return company is null
            ? ApiResponse<OrganizationDto>.Fail("Organization not found.")
            : ApiResponse<OrganizationDto>.Ok(MapOrganization(company));
    }

    public async Task<ApiResponse<OrganizationDto>> UpdateCurrentAsync(UpdateCurrentOrganizationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenantCompanyId(out var companyId, out var error))
        {
            return ApiResponse<OrganizationDto>.Fail(error);
        }

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return ApiResponse<OrganizationDto>.Fail("Organization not found.");
        }

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return ApiResponse<OrganizationDto>.Fail("Organization name is required.");
        }

        var targetSlug = string.IsNullOrWhiteSpace(request.Slug)
            ? company.Slug
            : Slugify(request.Slug);
        if (string.IsNullOrWhiteSpace(targetSlug))
        {
            return ApiResponse<OrganizationDto>.Fail("Organization slug is invalid.");
        }

        var normalizedSlug = targetSlug.ToUpperInvariant();
        var slugExists = await dbContext.Companies
            .AsNoTracking()
            .AnyAsync(item => item.Id != company.Id && item.NormalizedSlug == normalizedSlug, cancellationToken);
        if (slugExists)
        {
            return ApiResponse<OrganizationDto>.Fail("Organization slug already exists.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && !string.Equals(request.Status, company.Status, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetRoleForCurrentTenant(out var actorRole, out error) || actorRole < OrganizationRole.Owner)
            {
                return ApiResponse<OrganizationDto>.Fail(error ?? "Only organization owners can change organization status.");
            }

            company.Status = request.Status.Trim();
            company.IsActive = !string.Equals(company.Status, "Inactive", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(company.Status, "Suspended", StringComparison.OrdinalIgnoreCase);
        }

        company.Name = normalizedName;
        company.Slug = targetSlug;
        company.NormalizedSlug = normalizedSlug;
        company.BrandingLogoUrl = NormalizeOptional(request.BrandingLogoUrl);
        company.BrandingPrimaryColor = NormalizeOptional(request.BrandingPrimaryColor);
        company.OnboardingState = NormalizeOptional(request.OnboardingState);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OrganizationDto>.Ok(MapOrganization(company), "Organization updated successfully.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<OrganizationMemberDto>>> GetCurrentMembersAsync(CancellationToken cancellationToken)
    {
        if (!TryGetTenantCompanyId(out var companyId, out var error))
        {
            return ApiResponse<IReadOnlyCollection<OrganizationMemberDto>>.Fail(error);
        }

        var members = await dbContext.OrganizationMembers
            .AsNoTracking()
            .Include(member => member.User)
            .Where(member => member.CompanyId == companyId && member.Status == OrganizationMemberStatus.Active.ToStorageValue())
            .OrderByDescending(member => member.Role)
            .ThenBy(member => member.User.FullName)
            .Select(member => new OrganizationMemberDto
            {
                UserId = member.UserId,
                FullName = member.User.FullName,
                Email = member.User.Email,
                Role = member.Role,
                Status = member.Status,
                JoinedAt = member.JoinedAt,
                InvitedByUserId = member.InvitedByUserId
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<IReadOnlyCollection<OrganizationMemberDto>>.Ok(members);
    }

    public async Task<ApiResponse<OrganizationMemberDto>> UpdateMemberRoleAsync(Guid memberUserId, UpdateOrganizationMemberRoleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetTenantCompanyId(out var companyId, out var error))
        {
            return ApiResponse<OrganizationMemberDto>.Fail(error);
        }

        if (!TryGetRoleForCurrentTenant(out var actorRole, out error))
        {
            return ApiResponse<OrganizationMemberDto>.Fail(error ?? "Active organization membership is required.");
        }

        if (!OrganizationRoleExtensions.TryParse(request.Role, out var targetRole))
        {
            return ApiResponse<OrganizationMemberDto>.Fail("Role is invalid.");
        }

        var member = await dbContext.OrganizationMembers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId
                && item.UserId == memberUserId
                && item.Status == OrganizationMemberStatus.Active.ToStorageValue(), cancellationToken);
        if (member is null)
        {
            return ApiResponse<OrganizationMemberDto>.Fail("Organization member not found.");
        }

        if (!OrganizationRoleExtensions.TryParse(member.Role, out var currentRole))
        {
            currentRole = OrganizationRole.ReadOnly;
        }

        if (!CanManageRole(actorRole, currentRole, targetRole))
        {
            return ApiResponse<OrganizationMemberDto>.Fail("You are not allowed to change this member role.");
        }

        member.Role = targetRole.ToStorageValue();

        if (targetRole == OrganizationRole.Owner)
        {
            var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
            if (company is not null)
            {
                company.OwnerUserId = memberUserId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<OrganizationMemberDto>.Ok(new OrganizationMemberDto
        {
            UserId = member.UserId,
            FullName = member.User.FullName,
            Email = member.User.Email,
            Role = member.Role,
            Status = member.Status,
            JoinedAt = member.JoinedAt,
            InvitedByUserId = member.InvitedByUserId
        }, "Organization member role updated successfully.");
    }

    public async Task<ApiResponse<object>> RemoveMemberAsync(Guid memberUserId, CancellationToken cancellationToken)
    {
        if (!TryGetTenantCompanyId(out var companyId, out var error))
        {
            return ApiResponse<object>.Fail(error);
        }

        if (!TryGetRoleForCurrentTenant(out var actorRole, out error))
        {
            return ApiResponse<object>.Fail(error ?? "Active organization membership is required.");
        }

        var actorUserId = tenantContext.UserId;
        if (actorUserId is null || actorUserId == Guid.Empty)
        {
            return ApiResponse<object>.Fail("Authenticated user context is required.");
        }

        var member = await dbContext.OrganizationMembers
            .FirstOrDefaultAsync(item => item.CompanyId == companyId
                && item.UserId == memberUserId
                && item.Status == OrganizationMemberStatus.Active.ToStorageValue(), cancellationToken);
        if (member is null)
        {
            return ApiResponse<object>.Fail("Organization member not found.");
        }

        if (!OrganizationRoleExtensions.TryParse(member.Role, out var memberRole))
        {
            memberRole = OrganizationRole.ReadOnly;
        }

        if (memberRole == OrganizationRole.Owner)
        {
            var ownerCount = await dbContext.OrganizationMembers
                .CountAsync(item => item.CompanyId == companyId
                    && item.Status == OrganizationMemberStatus.Active.ToStorageValue()
                    && item.Role == OrganizationRole.Owner.ToStorageValue(), cancellationToken);
            if (ownerCount <= 1)
            {
                return ApiResponse<object>.Fail("The sole organization owner cannot be removed.");
            }

            if (actorRole != OrganizationRole.Owner)
            {
                return ApiResponse<object>.Fail("Only an organization owner can remove another owner.");
            }
        }

        if (actorRole < OrganizationRole.Administrator)
        {
            return ApiResponse<object>.Fail("Only administrators and owners can remove members.");
        }

        if (actorRole != OrganizationRole.Owner && memberRole >= OrganizationRole.Administrator)
        {
            return ApiResponse<object>.Fail("Only owners can remove administrators or owners.");
        }

        if (memberUserId == actorUserId && actorRole == OrganizationRole.Owner)
        {
            var ownerCount = await dbContext.OrganizationMembers
                .CountAsync(item => item.CompanyId == companyId
                    && item.Status == OrganizationMemberStatus.Active.ToStorageValue()
                    && item.Role == OrganizationRole.Owner.ToStorageValue(), cancellationToken);
            if (ownerCount <= 1)
            {
                return ApiResponse<object>.Fail("The sole organization owner cannot remove themselves.");
            }
        }

        member.Status = OrganizationMemberStatus.Removed.ToStorageValue();
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<object>.Ok(new { memberUserId }, "Organization member removed.");
    }

    private bool TryGetTenantCompanyId(out Guid companyId, out string error)
    {
        companyId = tenantContext.CompanyId ?? Guid.Empty;
        if (companyId == Guid.Empty)
        {
            error = "Authenticated tenant context is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryGetRoleForCurrentTenant(out OrganizationRole role, out string? error)
    {
        role = OrganizationRole.ReadOnly;
        error = null;

        if (tenantContext.CompanyId is not { } companyId || companyId == Guid.Empty || tenantContext.UserId is not { } userId || userId == Guid.Empty)
        {
            error = "Authenticated tenant membership is required.";
            return false;
        }

        var membershipRole = dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.UserId == userId
                && item.Status == OrganizationMemberStatus.Active.ToStorageValue())
            .Select(item => item.Role)
            .FirstOrDefault();

        if (!OrganizationRoleExtensions.TryParse(membershipRole, out role))
        {
            error = "Active organization membership is required.";
            return false;
        }

        return true;
    }

    private static bool CanManageRole(OrganizationRole actorRole, OrganizationRole currentRole, OrganizationRole targetRole)
    {
        if (actorRole == OrganizationRole.Owner)
        {
            return true;
        }

        if (actorRole == OrganizationRole.Administrator)
        {
            return currentRole < OrganizationRole.Administrator && targetRole < OrganizationRole.Owner;
        }

        if (actorRole == OrganizationRole.Manager)
        {
            return currentRole <= OrganizationRole.Host && targetRole <= OrganizationRole.Host;
        }

        return false;
    }

    private static OrganizationDto MapOrganization(Company company)
    {
        return new OrganizationDto
        {
            Id = company.Id,
            Name = company.Name,
            Slug = company.Slug,
            Status = company.Status,
            OwnerUserId = company.OwnerUserId,
            BrandingLogoUrl = company.BrandingLogoUrl,
            BrandingPrimaryColor = company.BrandingPrimaryColor,
            OnboardingState = company.OnboardingState,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };
    }

    private static string Slugify(string input)
    {
        var chars = input.Trim().ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : '-').ToArray();
        var collapsed = new string(chars);
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        return collapsed.Trim('-');
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}