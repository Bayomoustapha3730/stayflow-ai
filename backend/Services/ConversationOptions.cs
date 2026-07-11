namespace StayFlow.Api.Services;

public sealed class ConversationOptions
{
    public const string SectionName = "Conversation";

    public int MaxMessageCharacters { get; init; } = 2000;
    public int ReuseOpenConversationMinutes { get; init; } = 120;
    public int MaxHistoryMessages { get; init; } = 100;
    public bool AllowResolvedConversationReopen { get; init; }
}
