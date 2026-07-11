using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;

namespace StayFlow.Api.DTOs.Chat;

public sealed record SendChatMessageRequest
{
    public Guid? ConversationId { get; init; }
    public Guid GuestId { get; init; }
    public Guid? ReservationId { get; init; }
    public Guid? PropertyId { get; init; }
    public string Message { get; init; } = string.Empty;
    public GuestChannel Channel { get; init; } = GuestChannel.Web;
    public string? ChannelIdentity { get; init; }
    public string? ExternalMessageId { get; init; }
    public string? ExplicitReservationReference { get; init; }
    public string? ExplicitPropertyName { get; init; }
    public DateTimeOffset? CurrentTimestamp { get; init; }
}

public sealed class ChatHistoryQueryParameters : PaginationQuery;

public sealed record EscalateChatRequest
{
    public Guid GuestId { get; init; }
    public string? Reason { get; init; }
}

public sealed record EndChatRequest
{
    public Guid GuestId { get; init; }
}

public sealed class ChatMessageResponse
{
    public Guid ConversationId { get; init; }
    public ConversationStatus ConversationStatus { get; init; }
    public ChatVisibleMessageDto GuestMessage { get; init; } = null!;
    public ChatVisibleMessageDto? AssistantMessage { get; init; }
    public bool HumanTakeoverEnabled { get; init; }
    public bool RequiresHostAttention { get; init; }
    public string? EscalationReason { get; init; }
    public ChatProviderMetadataResponse? ProviderMetadata { get; init; }
    public IReadOnlyCollection<QuestionContextCategory> QuestionCategories { get; init; } = [];
    public IReadOnlyCollection<ReservationCandidateLabel> CandidateLabels { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ChatConversationResponse
{
    public Guid ConversationId { get; init; }
    public ConversationStatus Status { get; init; }
    public GuestChannel Channel { get; init; }
    public string? Subject { get; init; }
    public bool HumanTakeoverEnabled { get; init; }
    public bool RequiresHostAttention { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public ChatReservationSummary? Reservation { get; init; }
    public IReadOnlyCollection<ChatVisibleMessageDto> RecentMessages { get; init; } = [];
}

public sealed class ChatHistoryResponse
{
    public Guid ConversationId { get; init; }
    public PagedResult<ChatVisibleMessageDto> Messages { get; init; } = null!;
}

public sealed class ChatStatusResponse
{
    public Guid ConversationId { get; init; }
    public ConversationStatus Status { get; init; }
    public bool HumanTakeoverEnabled { get; init; }
    public bool RequiresHostAttention { get; init; }
    public string GuestSafeMessage { get; init; } = string.Empty;
}

public sealed class ChatVisibleMessageDto
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public ConversationSenderType SenderType { get; init; }
    public string Content { get; init; } = string.Empty;
    public ConversationMessageType MessageType { get; init; }
    public DateTimeOffset SentAt { get; init; }
}

public sealed class ChatProviderMetadataResponse
{
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public string? RequestId { get; init; }
}

public sealed class ChatReservationSummary
{
    public string? ConfirmationNumber { get; init; }
    public DateOnly? CheckInDate { get; init; }
    public DateOnly? CheckOutDate { get; init; }
    public string? PropertyDisplayName { get; init; }
}
