using System.Text.RegularExpressions;

namespace StayFlow.Api.Services;

public static class WhatsAppFailureMapper
{
    public static (string Category, string Summary) Map(string? failureCode, string? failureReason)
    {
        var code = Normalize(failureCode);
        var reason = Normalize(failureReason);
        var combined = $"{code} {reason}".Trim();

        if (Matches(combined, "rate", "throttle", "too many requests", "429"))
        {
            return ("RateLimited", "WhatsApp is receiving too many requests. Try again shortly.");
        }

        if (Matches(combined, "auth", "token", "permission", "forbidden", "unauthorized", "phone_number_id", "waba", "configuration", "credential", "signature"))
        {
            return ("AuthenticationOrConfigurationIssue", "WhatsApp sending is unavailable. Contact an administrator.");
        }

        if (Matches(combined, "invalid", "malformed", "format", "destination", "phone"))
        {
            return ("InvalidDestination", "This WhatsApp destination is invalid.");
        }

        if (Matches(combined, "opt out", "opted out", "recipient", "not a whatsapp", "unavailable", "blocked"))
        {
            return ("RecipientUnavailable", "This WhatsApp recipient is unavailable.");
        }

        if (Matches(combined, "temporary", "timeout", "timed out", "unavailable", "internal", "service"))
        {
            return ("TemporaryProviderIssue", "WhatsApp is temporarily unavailable. Try again.");
        }

        return ("UnknownDeliveryFailure", "WhatsApp could not deliver this message.");
    }

    private static bool Matches(string value, params string[] patterns)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return patterns.Any(pattern => Regex.IsMatch(value, Regex.Escape(pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
