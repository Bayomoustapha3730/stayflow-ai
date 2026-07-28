namespace StayFlow.Api.Services.ConciergeActions;

public sealed class ConciergeActionConfirmationService : IConciergeActionConfirmationService
{
    public bool IsAffirmative(string message)
    {
        var normalized = Normalize(message);
        return normalized is "yes" or "confirm" or "submit" or "submit it" or "go ahead" or "ok" or "okay";
    }

    public bool IsNegative(string message)
    {
        var normalized = Normalize(message);
        return normalized is "no" or "nope";
    }

    public bool IsCancel(string message)
    {
        var normalized = Normalize(message);
        return normalized is "cancel" or "never mind" or "nevermind";
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
