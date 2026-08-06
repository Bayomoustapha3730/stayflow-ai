namespace StayFlow.Api.Authorization;

public static class ApiKeyAuthenticationDefaults
{
    public const string Scheme = "ApiKey";
}

public static class ApiKeyPolicyNames
{
    public const string IntegrationsRead = "apikey.integrations.read";
    public const string UsageRead = "apikey.usage.read";
    public const string ConversationsRead = "apikey.conversations.read";
}