using StayFlow.Api.Common;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;

namespace StayFlow.Api.DTOs.Conversations;

public sealed class CreateConversationRequest
{
    public Guid GuestId { get; init; }
    public Guid? ReservationId { get; init; }
    public Guid? PropertyId { get; init; }
    public GuestChannel Channel { get; init; } = GuestChannel.Web;
    public string? ChannelIdentity { get; init; }
    public string? Subject { get; init; }
    public Guid? AssignedUserId { get; init; }
}

public sealed class AddGuestMessageRequest
{
    public string Content { get; init; } = string.Empty;
    public string? ExternalMessageId { get; init; }
    public DateTimeOffset? SentAt { get; init; }
}

public sealed class AddHostMessageRequest
{
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset? SentAt { get; init; }
}

public sealed class AddInternalNoteRequest
{
    public string Content { get; init; } = string.Empty;
}

public sealed class EscalateConversationRequest
{
    public string? Reason { get; init; }
}

public sealed class ConversationHistoryQueryParameters : PaginationQuery
{
    public bool IncludeInternal { get; init; }
}

public class ConversationSummaryResponse
{
    public Guid Id { get; init; }
    public Guid GuestId { get; init; }
    public Guid? ReservationId { get; init; }
    public Guid? PropertyId { get; init; }
    public GuestChannel Channel { get; init; }
    public string? ChannelIdentity { get; init; }
    public ConversationStatus Status { get; init; }
    public string? Subject { get; init; }
    public bool HumanTakeoverEnabled { get; init; }
    public string? EscalationReason { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
}

public sealed class ConversationDetailResponse : ConversationSummaryResponse
{
    public ConversationGuestSummary Guest { get; init; } = null!;
    public ConversationReservationSummary? Reservation { get; init; }
    public ConversationPropertySummary? Property { get; init; }
    public ConversationAssignedUserSummary? AssignedUser { get; init; }
    public IReadOnlyCollection<ConversationMessageResponse> Messages { get; init; } = [];
}

public sealed class ConversationMessageResponse
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public ConversationSenderType SenderType { get; init; }
    public ConversationMessageType MessageType { get; init; }
    public string Content { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
    public DateTimeOffset SentAt { get; init; }
}

public sealed class ConversationHistoryResponse
{
    public Guid ConversationId { get; init; }
    public PagedResult<ConversationMessageResponse> Messages { get; init; } = null!;
}

public sealed class ConversationGuestSummary
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string PreferredLanguage { get; init; } = string.Empty;
}

public sealed class ConversationReservationSummary
{
    public Guid Id { get; init; }
    public string? ConfirmationNumber { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public ReservationStatus Status { get; init; }
}

public sealed class ConversationPropertySummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
}

public sealed class ConversationAssignedUserSummary
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
}
