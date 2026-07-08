using System.Text.Json;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Reservations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class ReservationService(
    IReservationRepository reservationRepository,
    ICurrentTenantContext currentTenantContext,
    IReservationStatusTransitionPolicy statusTransitionPolicy) : IReservationService
{
    public async Task<ApiResponse<PagedResult<ReservationSummaryDto>>> GetAsync(ReservationQueryParameters query, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PagedResult<ReservationSummaryDto>>.Fail(tenantError, [tenantError]);
        }

        var reservations = await reservationRepository.GetAsync(companyId, query, cancellationToken);

        return ApiResponse<PagedResult<ReservationSummaryDto>>.Ok(new PagedResult<ReservationSummaryDto>
        {
            Items = reservations.Items.Select(MapToSummaryDto).ToList(),
            PageNumber = reservations.PageNumber,
            PageSize = reservations.PageSize,
            TotalCount = reservations.TotalCount
        });
    }

    public async Task<ApiResponse<ReservationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ReservationDto>.Fail(tenantError, [tenantError]);
        }

        var reservation = await reservationRepository.GetByIdAsync(id, companyId, cancellationToken);
        return reservation is null
            ? ApiResponse<ReservationDto>.Fail("Reservation was not found.")
            : ApiResponse<ReservationDto>.Ok(MapToDto(reservation));
    }

    public async Task<ApiResponse<ReservationDto>> CreateAsync(CreateReservationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ReservationDto>.Fail(tenantError, [tenantError]);
        }

        var validation = ReservationRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ApiResponse<ReservationDto>.Fail("Reservation validation failed.", validation.Errors);
        }

        var associationValidation = await ValidateTenantAssociationsAsync(companyId, request.PropertyId, request.PrimaryGuestId, cancellationToken);
        if (!associationValidation.Success)
        {
            return associationValidation;
        }

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = request.PropertyId,
            PrimaryGuestId = request.PrimaryGuestId,
            ExternalReservationReference = NormalizeOptional(request.ExternalReservationReference),
            ReservationSource = request.ReservationSource.Trim(),
            ConfirmationNumber = NormalizeOptional(request.ConfirmationNumber),
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
            Adults = request.Adults,
            Children = request.Children,
            TotalGuestCount = request.Adults + request.Children,
            Status = ReservationStatus.Draft,
            Currency = NormalizeCurrency(request.Currency),
            BookingAmount = request.BookingAmount,
            SpecialRequests = NormalizeOptional(request.SpecialRequests),
            InternalNotes = NormalizeOptional(request.InternalNotes),
            IsActive = true
        };

        await reservationRepository.AddAsync(reservation, cancellationToken);
        await AddAuditLogAsync("Created", reservation, cancellationToken);
        await reservationRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<ReservationDto>.Ok(MapToDto(reservation), "Reservation created successfully.");
    }

    public async Task<ApiResponse<ReservationDto>> UpdateAsync(Guid id, UpdateReservationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ReservationDto>.Fail(tenantError, [tenantError]);
        }

        var validation = ReservationRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ApiResponse<ReservationDto>.Fail("Reservation validation failed.", validation.Errors);
        }

        var reservation = await reservationRepository.GetByIdAsync(id, companyId, cancellationToken);
        if (reservation is null)
        {
            return ApiResponse<ReservationDto>.Fail("Reservation was not found.");
        }

        var associationValidation = await ValidateTenantAssociationsAsync(companyId, request.PropertyId, request.PrimaryGuestId, cancellationToken);
        if (!associationValidation.Success)
        {
            return associationValidation;
        }

        reservation.PropertyId = request.PropertyId;
        reservation.PrimaryGuestId = request.PrimaryGuestId;
        reservation.ExternalReservationReference = NormalizeOptional(request.ExternalReservationReference);
        reservation.ReservationSource = request.ReservationSource.Trim();
        reservation.ConfirmationNumber = NormalizeOptional(request.ConfirmationNumber);
        reservation.CheckInDate = request.CheckInDate;
        reservation.CheckOutDate = request.CheckOutDate;
        reservation.Adults = request.Adults;
        reservation.Children = request.Children;
        reservation.TotalGuestCount = request.Adults + request.Children;
        reservation.Currency = NormalizeCurrency(request.Currency);
        reservation.BookingAmount = request.BookingAmount;
        reservation.SpecialRequests = NormalizeOptional(request.SpecialRequests);
        reservation.InternalNotes = NormalizeOptional(request.InternalNotes);
        reservation.IsActive = request.IsActive;

        await AddAuditLogAsync("Updated", reservation, cancellationToken);
        await reservationRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<ReservationDto>.Ok(MapToDto(reservation), "Reservation updated successfully.");
    }

    public async Task<ApiResponse<ReservationDto>> TransitionStatusAsync(Guid id, TransitionReservationStatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<ReservationDto>.Fail(tenantError, [tenantError]);
        }

        if (!TryParseStatus(request.TargetStatus, out var targetStatus))
        {
            return ApiResponse<ReservationDto>.Fail("Reservation status transition failed.", ["Target status is invalid."]);
        }

        var reservation = await reservationRepository.GetByIdAsync(id, companyId, cancellationToken);
        if (reservation is null)
        {
            return ApiResponse<ReservationDto>.Fail("Reservation was not found.");
        }

        var previousStatus = reservation.Status;
        if (previousStatus == targetStatus)
        {
            return ApiResponse<ReservationDto>.Ok(MapToDto(reservation), "Reservation status is already current.");
        }

        if (!statusTransitionPolicy.CanTransition(previousStatus, targetStatus))
        {
            return ApiResponse<ReservationDto>.Fail(
                "Reservation status transition failed.",
                [$"Cannot transition reservation status from {previousStatus} to {targetStatus}."]);
        }

        reservation.Status = targetStatus;

        await AddStatusTransitionAuditLogAsync(previousStatus, targetStatus, reservation, cancellationToken);
        await reservationRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<ReservationDto>.Ok(MapToDto(reservation), "Reservation status updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<object>.Fail(tenantError, [tenantError]);
        }

        var reservation = await reservationRepository.GetByIdAsync(id, companyId, cancellationToken);
        if (reservation is null)
        {
            return ApiResponse<object>.Fail("Reservation was not found.");
        }

        reservation.IsDeleted = true;
        reservation.DeletedAt = DateTimeOffset.UtcNow;
        reservation.DeletedBy = currentTenantContext.UserId;

        await AddAuditLogAsync("Deleted", reservation, cancellationToken);
        await reservationRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { reservation.Id }, "Reservation deleted successfully.");
    }

    private async Task<ApiResponse<ReservationDto>> ValidateTenantAssociationsAsync(Guid companyId, Guid propertyId, Guid primaryGuestId, CancellationToken cancellationToken)
    {
        if (!await reservationRepository.CompanyExistsAsync(companyId, cancellationToken))
        {
            return ApiResponse<ReservationDto>.Fail("Company was not found.");
        }

        if (!await reservationRepository.PropertyBelongsToCompanyAsync(propertyId, companyId, cancellationToken))
        {
            return ApiResponse<ReservationDto>.Fail("Property was not found.");
        }

        if (!await reservationRepository.GuestBelongsToCompanyAsync(primaryGuestId, companyId, cancellationToken))
        {
            return ApiResponse<ReservationDto>.Fail("Guest was not found.");
        }

        return ApiResponse<ReservationDto>.Ok(null!);
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

    private async Task AddAuditLogAsync(string action, Reservation reservation, CancellationToken cancellationToken)
    {
        await reservationRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Reservation),
            EntityId = reservation.Id,
            Action = action,
            Details = JsonSerializer.Serialize(new
            {
                reservation.CompanyId,
                reservation.PropertyId,
                reservation.PrimaryGuestId,
                reservation.ReservationSource,
                reservation.ConfirmationNumber,
                reservation.CheckInDate,
                reservation.CheckOutDate,
                Status = reservation.Status.ToString(),
                reservation.IsActive,
                reservation.IsDeleted,
                reservation.DeletedAt,
                reservation.DeletedBy,
                AuthenticatedUserId = currentTenantContext.UserId,
                currentTenantContext.CorrelationId
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static ReservationSummaryDto MapToSummaryDto(Reservation reservation)
    {
        return new ReservationSummaryDto
        {
            Id = reservation.Id,
            PropertyId = reservation.PropertyId,
            PrimaryGuestId = reservation.PrimaryGuestId,
            ReservationSource = reservation.ReservationSource,
            ConfirmationNumber = reservation.ConfirmationNumber,
            CheckInDate = reservation.CheckInDate,
            CheckOutDate = reservation.CheckOutDate,
            TotalGuestCount = reservation.TotalGuestCount,
            Status = reservation.Status.ToString(),
            IsActive = reservation.IsActive,
            CreatedAt = reservation.CreatedAt
        };
    }

    private static ReservationDto MapToDto(Reservation reservation)
    {
        return new ReservationDto
        {
            Id = reservation.Id,
            PropertyId = reservation.PropertyId,
            PrimaryGuestId = reservation.PrimaryGuestId,
            ExternalReservationReference = reservation.ExternalReservationReference,
            ReservationSource = reservation.ReservationSource,
            ConfirmationNumber = reservation.ConfirmationNumber,
            CheckInDate = reservation.CheckInDate,
            CheckOutDate = reservation.CheckOutDate,
            Adults = reservation.Adults,
            Children = reservation.Children,
            TotalGuestCount = reservation.TotalGuestCount,
            Status = reservation.Status.ToString(),
            Currency = reservation.Currency,
            BookingAmount = reservation.BookingAmount,
            SpecialRequests = reservation.SpecialRequests,
            InternalNotes = reservation.InternalNotes,
            IsActive = reservation.IsActive,
            CreatedAt = reservation.CreatedAt,
            UpdatedAt = reservation.UpdatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeCurrency(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private async Task AddStatusTransitionAuditLogAsync(
        ReservationStatus previousStatus,
        ReservationStatus newStatus,
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        await reservationRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Reservation),
            EntityId = reservation.Id,
            Action = "StatusTransitioned",
            Details = JsonSerializer.Serialize(new
            {
                reservation.CompanyId,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = newStatus.ToString(),
                AuthenticatedUserId = currentTenantContext.UserId,
                currentTenantContext.CorrelationId
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static bool TryParseStatus(string? value, out ReservationStatus status)
    {
        return Enum.TryParse(value?.Trim(), ignoreCase: true, out status)
            && Enum.IsDefined(status);
    }
}
