namespace StayFlow.Api.Models;

public static class ApiKeyScope
{
    public const string IntegrationsRead = "integrations.read";
    public const string UsageRead = "usage.read";
    public const string ConversationsRead = "conversations.read";

    public static readonly IReadOnlyCollection<string> All =
    [
        IntegrationsRead,
        UsageRead,
        ConversationsRead
    ];
}