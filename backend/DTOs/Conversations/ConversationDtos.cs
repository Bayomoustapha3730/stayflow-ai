using StayFlow.Api.Common;
<<<<<<< HEAD
using StayFlow.Api.DTOs.ReservationContext;
=======
>>>>>>> 297967c (Implement host conversation inbox endpoint)
using StayFlow.Api.Models;

namespace StayFlow.Api.DTOs.Conversations;

<<<<<<< HEAD
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
=======
public sealed class ConversationListQueryParameters : PaginationQuery
{
    private const int MaxListPageSize = 100;

    public int Page { get; init; } = 1;
    public new int PageSize { get; init; } = 25;
    public ConversationStatus? Status { get; init; }
    public Guid? PropertyId { get; init; }
    public bool? RequiresHostAttention { get; init; }
    public string? Search { get; init; }

    public new int NormalizedPageSize => PageSize switch
    {
        < 1 => 25,
        > MaxListPageSize => MaxListPageSize,
        _ => PageSize
    };
}

public sealed class ConversationListResponse
{
    public IReadOnlyCollection<ConversationSummaryResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public sealed class ConversationSummaryResponse
{
    public Guid ConversationId { get; init; }
    public ConversationStatus Status { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string? Subject { get; init; }
    public ConversationGuestSummary? Guest { get; init; }
    public ConversationPropertySummary? Property { get; init; }
    public ConversationReservationSummary? Reservation { get; init; }
    public ConversationAssignedUserSummary? AssignedUser { get; init; }
    public bool HumanTakeoverEnabled { get; init; }
    public bool RequiresHostAttention { get; init; }
>>>>>>> 297967c (Implement host conversation inbox endpoint)
    public string? EscalationReason { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
<<<<<<< HEAD
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
=======
    public string? LatestVisibleMessagePreview { get; init; }
    public ConversationSenderType? LatestVisibleMessageSenderType { get; init; }
    public DateTimeOffset? LatestVisibleMessageTimestamp { get; init; }
    public int TotalVisibleMessageCount { get; init; }
>>>>>>> 297967c (Implement host conversation inbox endpoint)
}

public sealed class ConversationGuestSummary
{
<<<<<<< HEAD
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
=======
    public Guid GuestId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Email { get; init; }
>>>>>>> 297967c (Implement host conversation inbox endpoint)
}

public sealed class ConversationPropertySummary
{
<<<<<<< HEAD
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
=======
    public Guid PropertyId { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class ConversationReservationSummary
{
    public Guid ReservationId { get; init; }
    public string? ConfirmationNumber { get; init; }
>>>>>>> 297967c (Implement host conversation inbox endpoint)
}

public sealed class ConversationAssignedUserSummary
{
<<<<<<< HEAD
    public Guid Id { get; init; }
=======
    public Guid UserId { get; init; }
>>>>>>> 297967c (Implement host conversation inbox endpoint)
    public string FullName { get; init; } = string.Empty;
}
