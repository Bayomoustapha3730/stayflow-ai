using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.AIResponseValidation;
using StayFlow.Api.DTOs.ReservationContext;

namespace StayFlow.Api.DTOs.Chat;

public sealed class SendChatMessageRequest
{
    public Guid? ConversationId { get; init; }
    public Guid? GuestId { get; init; }
    public Guid? PropertyId { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Channel { get; init; }
    public string? ChannelIdentity { get; init; }
    public string? ExplicitReservationReference { get; init; }
    public string? ExplicitPropertyName { get; init; }
    public DateTimeOffset CurrentTimestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ChatHistoryQueryParameters : PaginationQuery
{
    public Guid ConversationId { get; init; }
}

public sealed class EscalateChatRequest
{
    public Guid ConversationId { get; init; }
    public string? Reason { get; init; }
}

public sealed class EndChatRequest
{
    public Guid ConversationId { get; init; }
}

public sealed class ChatMessageDto
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public string SenderType { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? AIOutcome { get; init; }
    public string? EscalationReason { get; init; }
    public bool IsInternal { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ChatMessageResponseDto
{
    public Guid ConversationId { get; init; }
    public ChatMessageDto GuestMessage { get; init; } = null!;
    public ChatMessageDto AssistantMessage { get; init; } = null!;
    public AIOrchestrationOutcome Outcome { get; init; }
    public IReadOnlyCollection<ReservationCandidateLabel> CandidateLabels { get; init; } = [];
    public IReadOnlyCollection<QuestionContextCategory> QuestionCategories { get; init; } = [];
    public IReadOnlyCollection<AIResponseViolationCode> ValidationViolations { get; init; } = [];
    public AIProviderMetadata? ProviderMetadata { get; init; }
}

public sealed class ChatStatusDto
{
    public Guid ConversationId { get; init; }
    public string Status { get; init; } = string.Empty;
}
