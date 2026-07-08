using System.Text.Json;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Guests;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class GuestService(IGuestRepository guestRepository, ICurrentTenantContext currentTenantContext) : IGuestService
{
    public async Task<ApiResponse<PagedResult<GuestSummaryDto>>> GetAsync(GuestQueryParameters query, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PagedResult<GuestSummaryDto>>.Fail(tenantError, [tenantError]);
        }

        var guests = await guestRepository.GetAsync(companyId, query, cancellationToken);

        return ApiResponse<PagedResult<GuestSummaryDto>>.Ok(new PagedResult<GuestSummaryDto>
        {
            Items = guests.Items.Select(MapToSummaryDto).ToList(),
            PageNumber = guests.PageNumber,
            PageSize = guests.PageSize,
            TotalCount = guests.TotalCount
        });
    }

    public async Task<ApiResponse<GuestDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<GuestDto>.Fail(tenantError, [tenantError]);
        }

        var guest = await guestRepository.GetByIdAsync(id, companyId, cancellationToken);
        return guest is null
            ? ApiResponse<GuestDto>.Fail("Guest was not found.")
            : ApiResponse<GuestDto>.Ok(MapToDto(guest));
    }

    public async Task<ApiResponse<GuestDto>> CreateAsync(CreateGuestRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<GuestDto>.Fail(tenantError, [tenantError]);
        }

        var validation = GuestRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ApiResponse<GuestDto>.Fail("Guest validation failed.", validation.Errors);
        }

        if (!await guestRepository.CompanyExistsAsync(companyId, cancellationToken))
        {
            return ApiResponse<GuestDto>.Fail("Company was not found.");
        }

        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = NormalizeEmail(request.Email),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            PreferredLanguage = NormalizeLanguage(request.PreferredLanguage),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            Notes = NormalizeOptional(request.Notes),
            IsActive = true
        };

        await guestRepository.AddAsync(guest, cancellationToken);
        await AddAuditLogAsync("Created", guest, cancellationToken);
        await guestRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<GuestDto>.Ok(MapToDto(guest), "Guest created successfully.");
    }

    public async Task<ApiResponse<GuestDto>> UpdateAsync(Guid id, UpdateGuestRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<GuestDto>.Fail(tenantError, [tenantError]);
        }

        var validation = GuestRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ApiResponse<GuestDto>.Fail("Guest validation failed.", validation.Errors);
        }

        var guest = await guestRepository.GetByIdAsync(id, companyId, cancellationToken);
        if (guest is null)
        {
            return ApiResponse<GuestDto>.Fail("Guest was not found.");
        }

        guest.FirstName = request.FirstName.Trim();
        guest.LastName = request.LastName.Trim();
        guest.Email = NormalizeEmail(request.Email);
        guest.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        guest.PreferredLanguage = NormalizeLanguage(request.PreferredLanguage);
        guest.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        guest.Notes = NormalizeOptional(request.Notes);
        guest.IsActive = request.IsActive;

        await AddAuditLogAsync("Updated", guest, cancellationToken);
        await guestRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<GuestDto>.Ok(MapToDto(guest), "Guest updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<object>.Fail(tenantError, [tenantError]);
        }

        var guest = await guestRepository.GetByIdAsync(id, companyId, cancellationToken);
        if (guest is null)
        {
            return ApiResponse<object>.Fail("Guest was not found.");
        }

        guest.IsDeleted = true;
        guest.DeletedAt = DateTimeOffset.UtcNow;
        guest.DeletedBy = currentTenantContext.UserId;

        await AddAuditLogAsync("Deleted", guest, cancellationToken);
        await guestRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { guest.Id }, "Guest deleted successfully.");
    }

    private bool TryGetCompanyId(out Guid companyId, out string error)
    {
        if (!currentTenantContext.IsAuthenticated)
        {
            companyId = Guid.Empty;
            error = "Authenticated tenant context is required.";
            return false;
        }

        if (currentTenantContext.CompanyId is not { } tenantCompanyId || tenantCompanyId == Guid.Empty)
        {
            companyId = Guid.Empty;
            error = "Authenticated tenant context is missing or invalid.";
            return false;
        }

        companyId = tenantCompanyId;
        error = string.Empty;
        return true;
    }

    private async Task AddAuditLogAsync(string action, Guest guest, CancellationToken cancellationToken)
    {
        await guestRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Guest),
            EntityId = guest.Id,
            Action = action,
            Details = JsonSerializer.Serialize(new
            {
                guest.CompanyId,
                guest.Email,
                guest.PhoneNumber,
                guest.IsActive,
                guest.IsDeleted,
                guest.DeletedAt,
                guest.DeletedBy,
                AuthenticatedUserId = currentTenantContext.UserId,
                currentTenantContext.CorrelationId
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static GuestSummaryDto MapToSummaryDto(Guest guest)
    {
        return new GuestSummaryDto
        {
            Id = guest.Id,
            FirstName = guest.FirstName,
            LastName = guest.LastName,
            Email = guest.Email,
            PhoneNumber = guest.PhoneNumber,
            PreferredLanguage = guest.PreferredLanguage,
            CountryCode = guest.CountryCode,
            IsActive = guest.IsActive,
            CreatedAt = guest.CreatedAt
        };
    }

    private static GuestDto MapToDto(Guest guest)
    {
        return new GuestDto
        {
            Id = guest.Id,
            FirstName = guest.FirstName,
            LastName = guest.LastName,
            Email = guest.Email,
            PhoneNumber = guest.PhoneNumber,
            PreferredLanguage = guest.PreferredLanguage,
            CountryCode = guest.CountryCode,
            Notes = guest.Notes,
            IsActive = guest.IsActive,
            CreatedAt = guest.CreatedAt,
            UpdatedAt = guest.UpdatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeLanguage(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
