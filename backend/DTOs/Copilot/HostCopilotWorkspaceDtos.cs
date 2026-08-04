namespace StayFlow.Api.DTOs.Copilot;

public sealed class HostCopilotWorkspaceResponse
{
    public DateTimeOffset GeneratedAt { get; init; }
    public int TotalOpenItems { get; init; }
    public int TotalBreachedSlaItems { get; init; }
    public IReadOnlyCollection<HostCopilotWorkItemDto> Items { get; init; } = [];
}

public sealed class HostCopilotWorkItemDto
{
    public Guid WorkItemId { get; init; }
    public Guid ConversationId { get; init; }
    public Guid PropertyId { get; init; }
    public Guid? ReservationId { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string GuestName { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public bool IsEmergency { get; init; }
    public string SafetyClassification { get; init; } = string.Empty;
    public string PriorityReason { get; init; } = string.Empty;
    public HostCopilotSlaStatusDto Sla { get; init; } = new();
    public HostCopilotOperationalSummaryDto Summary { get; init; } = new();
    public IReadOnlyCollection<HostCopilotTimelineEventDto> Timeline { get; init; } = [];
    public IReadOnlyCollection<HostCopilotRecommendationDto> Recommendations { get; init; } = [];
    public IReadOnlyCollection<HostCopilotPendingActionDto> PendingActions { get; init; } = [];
}

public sealed class HostCopilotSlaStatusDto
{
    public int MinutesSinceLatestGuestMessage { get; init; }
    public DateTimeOffset? ResponseDueAt { get; init; }
    public bool IsBreached { get; init; }
    public string AlertLevel { get; init; } = "none";
    public string AlertMessage { get; init; } = string.Empty;
}

public sealed class HostCopilotOperationalSummaryDto
{
    public string Headline { get; init; } = string.Empty;
    public string LastGuestIntent { get; init; } = string.Empty;
    public string LastGuestMessagePreview { get; init; } = string.Empty;
    public int OpenActionCount { get; init; }
    public int VisibleMessageCount { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
}

public sealed class HostCopilotTimelineEventDto
{
    public DateTimeOffset Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

public sealed class HostCopilotRecommendationDto
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public int Confidence { get; init; }
}

public sealed class HostCopilotPendingActionDto
{
    public Guid ActionId { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

public sealed class HostCopilotDraftGenerateRequest
{
    public string? Tone { get; init; }
    public string? HostInstruction { get; init; }
}

public sealed class HostCopilotDraftValidateRequest
{
    public string Draft { get; init; } = string.Empty;
}

public sealed class HostCopilotDraftSendRequest
{
    public string Draft { get; init; } = string.Empty;
}

public sealed class HostCopilotDraftValidationResponse
{
    public bool IsValid { get; init; }
    public IReadOnlyCollection<string> Errors { get; init; } = [];
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
}

public sealed class HostCopilotDraftResponse
{
    public Guid ConversationId { get; init; }
    public string Draft { get; init; } = string.Empty;
    public bool UsedDeterministicFallback { get; init; }
    public string GenerationMode { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public HostCopilotDraftValidationResponse Validation { get; init; } = new();
}
