namespace StayFlow.Api.Services.Payments;

public interface IKenyanPhoneNumberNormalizer
{
    bool TryNormalize(string? value, out string normalized);
}

public sealed class KenyanPhoneNumberNormalizer : IKenyanPhoneNumberNormalizer
{
    public bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (value.Any(character => !char.IsAsciiDigit(character) && !" +-().".Contains(character)))
        {
            return false;
        }

        if (digits.Length == 10 && digits[0] == '0' && digits[1] == '7')
        {
            digits = "254" + digits[1..];
        }
        else if (digits.Length == 12 && digits.StartsWith("2547", StringComparison.Ordinal))
        {
        }
        else
        {
            return false;
        }

        normalized = digits;
        return true;
    }
}
