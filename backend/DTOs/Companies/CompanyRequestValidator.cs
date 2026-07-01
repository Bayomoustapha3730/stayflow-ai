using System.Text.RegularExpressions;

namespace StayFlow.Api.DTOs.Companies;

public static partial class CompanyRequestValidator
{
    public static CompanyValidationResult Validate(CreateCompanyRequest request)
    {
        return ValidateCore(request.Name, request.Email, request.PhoneNumber, request.CountryCode, request.TimeZone);
    }

    public static CompanyValidationResult Validate(UpdateCompanyRequest request)
    {
        return ValidateCore(request.Name, request.Email, request.PhoneNumber, request.CountryCode, request.TimeZone);
    }

    private static CompanyValidationResult ValidateCore(
        string name,
        string email,
        string phoneNumber,
        string countryCode,
        string timeZone)
    {
        var result = new CompanyValidationResult();

        if (string.IsNullOrWhiteSpace(name))
        {
            result.Errors.Add("Company name is required.");
        }
        else if (name.Length > 160)
        {
            result.Errors.Add("Company name must be 160 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            result.Errors.Add("Email is required.");
        }
        else if (!EmailPattern().IsMatch(email))
        {
            result.Errors.Add("Email format is invalid.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            result.Errors.Add("Phone number is required.");
        }
        else if (!PhonePattern().IsMatch(phoneNumber))
        {
            result.Errors.Add("Phone number must be in international format, for example +254700000000.");
        }

        if (countryCode.Length != 2)
        {
            result.Errors.Add("Country code must be a two-letter ISO code.");
        }

        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Length > 80)
        {
            result.Errors.Add("Time zone is required and must be 80 characters or fewer.");
        }

        return result;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex PhonePattern();
}
