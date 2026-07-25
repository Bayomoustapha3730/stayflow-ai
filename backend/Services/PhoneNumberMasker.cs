namespace StayFlow.Api.Services;

public static class PhoneNumberMasker
{
    public static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return "****";
        }

        var prefixDigits = Math.Min(1, digits.Length);
        var suffixDigits = Math.Min(4, digits.Length);
        if (digits.Length <= prefixDigits + suffixDigits)
        {
            return "****";
        }

        var prefix = digits[..prefixDigits];
        var suffix = digits[^suffixDigits..];
        return $"+{prefix}******{suffix}";
    }
}