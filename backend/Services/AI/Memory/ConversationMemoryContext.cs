using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Memory;

public sealed record ConversationMemoryContext(
    IReadOnlyCollection<string> RecentUserMessages,
    IReadOnlyCollection<string> RecentAssistantMessages,
    GuestIntent? LastResolvedIntent,
    string? ActiveTopic,
    IReadOnlyCollection<string> PriorSelectedArticleIds,
    string? PendingClarification,
    PendingClarificationContext? PendingClarificationContext,
    IReadOnlyDictionary<string, string> ResolvedEntities,
    string ConversationSummary,
    bool WasTruncated,
    DateTimeOffset GeneratedAtUtc);
