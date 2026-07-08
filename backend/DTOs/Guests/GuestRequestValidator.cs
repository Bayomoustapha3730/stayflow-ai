using System.Text.RegularExpressions;

namespace StayFlow.Api.DTOs.Guests;

public static partial class GuestRequestValidator
{
    public static GuestValidationResult Validate(CreateGuestRequest request)
    {
        return ValidateCore(
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.PreferredLanguage,
            request.CountryCode,
            request.Notes);
    }

    public static GuestValidationResult Validate(UpdateGuestRequest request)
    {
        return ValidateCore(
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.PreferredLanguage,
            request.CountryCode,
            request.Notes);
    }

    private static GuestValidationResult ValidateCore(
        string firstName,
        string lastName,
        string? email,
        string? phoneNumber,
        string preferredLanguage,
        string countryCode,
        string? notes)
    {
        var result = new GuestValidationResult();

        AddRequired(result, firstName, "First name", 100);
        AddRequired(result, lastName, "Last name", 100);

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (email.Length > 254)
            {
                result.Errors.Add("Email must be 254 characters or fewer.");
            }
            else if (!EmailPattern().IsMatch(email))
            {
                result.Errors.Add("Email format is invalid.");
            }
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            if (phoneNumber.Length > 32)
            {
                result.Errors.Add("Phone number must be 32 characters or fewer.");
            }
            else if (!PhonePattern().IsMatch(phoneNumber))
            {
                result.Errors.Add("Phone number must be in international format, for example +254700000000.");
            }
        }

        if (string.IsNullOrWhiteSpace(preferredLanguage) || preferredLanguage.Length > 16 || !LanguagePattern().IsMatch(preferredLanguage))
        {
            result.Errors.Add("Preferred language is required and must be a valid language code.");
        }

        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Trim().Length != 2 || !CountryCodePattern().IsMatch(countryCode.Trim()))
        {
            result.Errors.Add("Country code must be a two-letter ISO code.");
        }

        if (!string.IsNullOrWhiteSpace(notes) && notes.Length > 2000)
        {
            result.Errors.Add("Notes must be 2000 characters or fewer.");
        }

        return result;
    }

    private static void AddRequired(GuestValidationResult result, string? value, string fieldName, int maxLength)
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

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^[a-zA-Z]{2,3}(-[a-zA-Z]{2})?$")]
    private static partial Regex LanguagePattern();

    [GeneratedRegex(@"^[a-zA-Z]{2}$")]
    private static partial Regex CountryCodePattern();
}
