using System.Text.RegularExpressions;

namespace StayFlow.Api.DTOs.Reservations;

public static partial class ReservationRequestValidator
{
    public static ReservationValidationResult Validate(CreateReservationRequest request)
    {
        return ValidateCore(
            request.PropertyId,
            request.PrimaryGuestId,
            request.ExternalReservationReference,
            request.ReservationSource,
            request.ConfirmationNumber,
            request.CheckInDate,
            request.CheckOutDate,
            request.Adults,
            request.Children,
            request.Status,
            request.Currency,
            request.BookingAmount,
            request.SpecialRequests,
            request.InternalNotes);
    }

    public static ReservationValidationResult Validate(UpdateReservationRequest request)
    {
        return ValidateCore(
            request.PropertyId,
            request.PrimaryGuestId,
            request.ExternalReservationReference,
            request.ReservationSource,
            request.ConfirmationNumber,
            request.CheckInDate,
            request.CheckOutDate,
            request.Adults,
            request.Children,
            request.Status,
            request.Currency,
            request.BookingAmount,
            request.SpecialRequests,
            request.InternalNotes);
    }

    private static ReservationValidationResult ValidateCore(
        Guid propertyId,
        Guid primaryGuestId,
        string? externalReservationReference,
        string reservationSource,
        string? confirmationNumber,
        DateOnly checkInDate,
        DateOnly checkOutDate,
        int adults,
        int children,
        string status,
        string? currency,
        decimal? bookingAmount,
        string? specialRequests,
        string? internalNotes)
    {
        var result = new ReservationValidationResult();

        if (propertyId == Guid.Empty)
        {
            result.Errors.Add("PropertyId is required.");
        }

        if (primaryGuestId == Guid.Empty)
        {
            result.Errors.Add("PrimaryGuestId is required.");
        }

        AddRequired(result, reservationSource, "Reservation source", 80);
        AddRequired(result, status, "Status", 40);

        if (checkInDate == default)
        {
            result.Errors.Add("Check-in date is required.");
        }

        if (checkOutDate == default)
        {
            result.Errors.Add("Check-out date is required.");
        }

        if (checkInDate != default && checkOutDate != default && checkOutDate <= checkInDate)
        {
            result.Errors.Add("Check-out date must be after check-in date.");
        }

        if (adults < 0)
        {
            result.Errors.Add("Adults must be zero or greater.");
        }

        if (children < 0)
        {
            result.Errors.Add("Children must be zero or greater.");
        }

        if (adults + children <= 0)
        {
            result.Errors.Add("Total guest count must be greater than zero.");
        }

        AddOptionalMaxLength(result, externalReservationReference, "External reservation reference", 160);
        AddOptionalMaxLength(result, confirmationNumber, "Confirmation number", 80);
        AddOptionalMaxLength(result, specialRequests, "Special requests", 2000);
        AddOptionalMaxLength(result, internalNotes, "Internal notes", 2000);

        if (!string.IsNullOrWhiteSpace(currency) && (currency.Trim().Length != 3 || !CurrencyPattern().IsMatch(currency.Trim())))
        {
            result.Errors.Add("Currency must be a three-letter ISO currency code.");
        }

        if (bookingAmount is < 0)
        {
            result.Errors.Add("Booking amount must be zero or greater.");
        }

        return result;
    }

    private static void AddRequired(ReservationValidationResult result, string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Errors.Add($"{fieldName} is required.");
            return;
        }

        if (value.Length > maxLength)
        {
            result.Errors.Add($"{fieldName} must be {maxLength} characters or fewer.");
        }
    }

    private static void AddOptionalMaxLength(ReservationValidationResult result, string? value, string fieldName, int maxLength)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length > maxLength)
        {
            result.Errors.Add($"{fieldName} must be {maxLength} characters or fewer.");
        }
    }

    [GeneratedRegex(@"^[a-zA-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
