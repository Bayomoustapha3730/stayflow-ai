namespace StayFlow.Api.Services;

internal static class EmailIdentityNormalizer
{
    public static string Normalize(string email)
    {
        return email.Trim(' ').ToUpperInvariant();
    }
}