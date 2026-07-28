using System.Text.RegularExpressions;

namespace StayFlow.Api.Services;

public static class WhatsAppFailureMapper
{
    public static (string Category, string Summary) Map(string? failureCode, string? failureReason)
    {
        return Map(failureCode, failureReason, null, null, null, null, null);
    }

    public static (string Category, string Summary) Map(
        string? failureCode,
        string? failureReason,
        int? httpStatusCode,
        int? providerCode,
        int? providerSubcode,
        bool? isTransient,
        Exception? exception)
    {
        var code = Normalize(failureCode);
        var reason = Normalize(failureReason);
        var combined = $"{code} {reason} {providerCode} {providerSubcode} {httpStatusCode}".Trim();

        if (exception is OperationCanceledException)
        {
            return ("TemporaryProviderFailure", "WhatsApp request timed out. Try again shortly.");
        }

        if (httpStatusCode == 429 || Matches(combined, "rate", "throttle", "too many requests", "130429"))
        {
            return ("RateLimited", "WhatsApp is temporarily rate limited. Try again shortly.");
        }

        if (httpStatusCode == 401 || Matches(combined, "oauth", "invalid_token", "190", "authentication"))
        {
            return ("Authentication", "WhatsApp authentication failed. Contact an administrator.");
        }

        if (httpStatusCode == 403 || Matches(combined, "permission", "forbidden", "authorization", "200", "10"))
        {
            return ("Authorization", "WhatsApp authorization failed. Contact an administrator.");
        }

        if (Matches(combined, "131026", "invalid recipient", "recipient", "not a whatsapp"))
        {
            return ("InvalidDestination", "This WhatsApp destination is invalid.");
        }

        if (Matches(combined, "132001", "132005", "template", "not found", "status"))
        {
            return ("InvalidTemplate", "The selected WhatsApp template is unavailable.");
        }

        if (Matches(combined, "132012", "132000", "parameter", "variables", "placeholder"))
        {
            return ("TemplateParameterMismatch", "Template variables do not match the approved WhatsApp template.");
        }

        if (Matches(combined, "131047", "service window", "24-hour", "customer service"))
        {
            return ("CustomerServiceWindowClosed", "The WhatsApp customer-service window is closed. Send an approved template.");
        }

        if (httpStatusCode == 400 || Matches(combined, "invalid", "malformed", "configuration", "phone_number_id", "waba"))
        {
            return ("Configuration", "WhatsApp configuration is incomplete or invalid.");
        }

        if (httpStatusCode is 500 or 502 or 503 or 504)
        {
            return ("ProviderUnavailable", "WhatsApp provider is temporarily unavailable. Try again shortly.");
        }

        if (isTransient == true || Matches(combined, "timeout", "temporary", "internal", "service unavailable"))
        {
            return ("TemporaryProviderFailure", "WhatsApp is temporarily unavailable. Try again shortly.");
        }

        return ("Unknown", "WhatsApp could not complete this request.");
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
