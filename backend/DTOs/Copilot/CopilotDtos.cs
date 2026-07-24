namespace StayFlow.Api.DTOs.Copilot;

public sealed class CopilotSourceDto
{
    public string SourceType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? RelevanceReason { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
}

public sealed class CopilotConfidenceDto
{
    public int Score { get; init; }
    public string Level { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Reasons { get; init; } = [];
    public IReadOnlyCollection<string> MissingContext { get; init; } = [];
}

public sealed class ConversationCopilotSummaryResponse
{
    public Guid ConversationId { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string? LatestGuestMessage { get; init; }
    public int VisibleMessageCount { get; init; }
    public CopilotConfidenceDto? Confidence { get; init; }
    public IReadOnlyCollection<CopilotSourceDto> Sources { get; init; } = [];
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
    public bool ContextTruncated { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
}

public sealed class ConversationCopilotSuggestionsResponse
{
    public Guid ConversationId { get; init; }
    public IReadOnlyCollection<string> SuggestedReplies { get; init; } = [];
    public int ContextMessageCount { get; init; }
    public CopilotConfidenceDto? Confidence { get; init; }
    public IReadOnlyCollection<CopilotSourceDto> Sources { get; init; } = [];
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
    public bool ContextTruncated { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
}

public sealed class CopilotSuggestReplyRequest
{
    public string? Guidance { get; init; }
    public bool IncludeInternalNotes { get; init; }
    public int MaxContextMessages { get; init; } = 12;
}

public sealed class CopilotProviderMetadataResponse
{
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public string? RequestId { get; init; }
}

public sealed class CopilotSuggestReplyResponse
{
    public Guid ConversationId { get; init; }
    public string SuggestedReply { get; init; } = string.Empty;
    public string? Rationale { get; init; }
    public int ContextMessageCount { get; init; }
    public bool IsFallback { get; init; }
    public CopilotProviderMetadataResponse? ProviderMetadata { get; init; }
    public CopilotConfidenceDto? Confidence { get; init; }
    public IReadOnlyCollection<CopilotSourceDto> Sources { get; init; } = [];
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
    public bool ContextTruncated { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
}