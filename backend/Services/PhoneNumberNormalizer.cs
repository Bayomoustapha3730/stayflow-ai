using System.Text;

namespace StayFlow.Api.Services;

public sealed class PhoneNumberNormalizer : IPhoneNumberNormalizer
{
    public bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);
        var hasPlusPrefix = false;

        foreach (var character in trimmed)
        {
            if (character == '+' && builder.Length == 0)
            {
                hasPlusPrefix = true;
                continue;
            }

            if (char.IsDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (character is ' ' or '-' or '(' or ')' or '.')
            {
                continue;
            }

            return false;
        }

        if (!hasPlusPrefix)
        {
            return false;
        }

        if (builder.Length is < 8 or > 15)
        {
            return false;
        }

        normalized = $"+{builder}";
        return true;
    }

    public string? Mask(string? value)
    {
        if (!TryNormalize(value, out var normalized))
        {
            return PhoneNumberMasker.Mask(value);
        }

        return normalized.Length <= 5
            ? "+****"
            : $"{normalized[..2]}******{normalized[^4..]}";
    }
}