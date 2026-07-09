namespace StayFlow.Api.Services;

public static class OpenAIProviderFailureCategories
{
    public const string Timeout = "Timeout";
    public const string RateLimited = "RateLimited";
    public const string Authentication = "Authentication";
    public const string InvalidRequest = "InvalidRequest";
    public const string ProviderServerError = "ProviderServerError";
    public const string Network = "Network";
    public const string Cancelled = "Cancelled";
    public const string EmptyResponse = "EmptyResponse";
    public const string UnexpectedResponse = "UnexpectedResponse";
    public const string Unknown = "Unknown";
}
