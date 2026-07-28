namespace StayFlow.Api.DTOs.Copilot;

public sealed class CopilotSourceDto
{
    public string SourceType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? RelevanceReason { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
}

public sealed class CopilotOrchestrationWarningDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = "warning";
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
    public string? DetectedIntent { get; init; }
    public CopilotConfidenceDto? Confidence { get; init; }
    public IReadOnlyCollection<CopilotSourceDto> Sources { get; init; } = [];
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
    public IReadOnlyCollection<CopilotOrchestrationWarningDto> OrchestrationWarnings { get; init; } = [];
    public string? Provider { get; init; }
    public bool IsMock { get; init; }
    public bool FallbackUsed { get; init; }
    public bool ContextTruncated { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
}

public sealed class CopilotSuggestReplyRequest
{
    public string? Guidance { get; init; }
    public string? Tone { get; init; }
    public string? HostDraft { get; init; }
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
    public string? Tone { get; init; }
    public string? DetectedIntent { get; init; }
    public string? Rationale { get; init; }
    public int ContextMessageCount { get; init; }
    public bool IsFallback { get; init; }
    public bool FallbackUsed { get; init; }
    public bool RequiresHumanReview { get; init; }
    public string? Provider { get; init; }
    public bool IsMock { get; init; }
    public CopilotProviderMetadataResponse? ProviderMetadata { get; init; }
    public CopilotConfidenceDto? Confidence { get; init; }
    public IReadOnlyCollection<CopilotSourceDto> Sources { get; init; } = [];
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
    public IReadOnlyCollection<CopilotOrchestrationWarningDto> OrchestrationWarnings { get; init; } = [];
    public bool ContextTruncated { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
}

public sealed class CopilotRetrievalDiagnosticsDto
{
    public string DetectedIntent { get; init; } = string.Empty;
    public bool IntentAmbiguous { get; init; }
    public int IntentConfidenceScore { get; init; }
    public int SecondaryIntentCount { get; init; }
    public int CandidateCount { get; init; }
    public int SelectedCount { get; init; }
    public string ConfidenceLevel { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = string.Empty;
    public bool ClarificationRequired { get; init; }
    public bool EscalationRecommended { get; init; }
    public IReadOnlyCollection<string> SelectedCategories { get; init; } = [];
    public IReadOnlyCollection<string> ClarificationChoices { get; init; } = [];
    public IReadOnlyCollection<string> WarningCodes { get; init; } = [];
    public IReadOnlyDictionary<string, string> EvaluationMetadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed class ConversationRetrievalDiagnosticsResponse
{
    public Guid ConversationId { get; init; }
    public CopilotRetrievalDiagnosticsDto Diagnostics { get; init; } = null!;
    public bool ContextTruncated { get; init; }
    public bool FallbackUsed { get; init; }
    public bool RequiresHumanReview { get; init; }
    public string Provider { get; init; } = string.Empty;
    public long DurationMilliseconds { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
}