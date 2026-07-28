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
    public ConversationMessageProvider Provider { get; init; } = ConversationMessageProvider.None;
    public ConversationMessageDeliveryStatus? DeliveryStatus { get; init; }
}

public sealed class AddHostMessageRequest
{
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset? SentAt { get; init; }
    public ConversationMessageProvider Provider { get; init; } = ConversationMessageProvider.None;
    public bool BypassCustomerServiceWindowPolicy { get; init; }
}

public sealed class AddInternalNoteRequest
{
    public string Content { get; init; } = string.Empty;
}

public sealed class AssignConversationRequest
{
    public Guid? UserId { get; init; }
}

public sealed class EscalateConversationRequest
{
    public string? Reason { get; init; }
}

public sealed class ConversationHistoryQueryParameters : PaginationQuery
{
    public bool IncludeInternal { get; init; }
}

public sealed class ConversationListQueryParameters : PaginationQuery
{
    private const int MaxListPageSize = 100;

    public int Page { get; init; } = 1;
    public new int PageSize { get; init; } = 25;
    public ConversationStatus? Status { get; init; }
    public GuestChannel? Channel { get; init; }
    public ConversationReadStateFilter? ReadState { get; init; }
    public bool? HasFailedOutboundMessage { get; init; }
    public Guid? PropertyId { get; init; }
    public bool? RequiresHostAttention { get; init; }
    public string? Search { get; init; }
    public Guid? HostUserId { get; init; }

    public new int NormalizedPageSize => PageSize switch
    {
        < 1 => 25,
        > MaxListPageSize => MaxListPageSize,
        _ => PageSize
    };
}

public enum ConversationReadStateFilter
{
    Unread = 0,
    Read = 1
}

public sealed class ConversationListResponse
{
    public IReadOnlyCollection<ConversationSummaryResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public int TotalUnreadCount { get; init; }
}

public class ConversationSummaryResponse
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public Guid GuestId { get; init; }
    public Guid? ReservationId { get; init; }
    public Guid? PropertyId { get; init; }
<<<<<<< HEAD
    public string Channel { get; init; } = string.Empty;
=======
    public GuestChannel Channel { get; init; }
>>>>>>> origin/main
    public string? ChannelIdentity { get; init; }
    public ConversationStatus Status { get; init; }
    public string? Subject { get; init; }
    public bool HumanTakeoverEnabled { get; init; }
    public bool RequiresHostAttention { get; init; }
<<<<<<< HEAD
    public ConversationGuestSummary? Guest { get; init; }
    public ConversationPropertySummary? Property { get; init; }
    public ConversationReservationSummary? Reservation { get; init; }
    public ConversationAssignedUserSummary? AssignedUser { get; init; }
=======
>>>>>>> origin/main
    public string? EscalationReason { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
<<<<<<< HEAD
=======
    public ConversationGuestSummary? Guest { get; init; }
    public ConversationReservationSummary? Reservation { get; init; }
    public ConversationPropertySummary? Property { get; init; }
    public ConversationAssignedUserSummary? AssignedUser { get; init; }
>>>>>>> origin/main
    public string? LatestVisibleMessagePreview { get; init; }
    public ConversationSenderType? LatestVisibleMessageSenderType { get; init; }
    public DateTimeOffset? LatestVisibleMessageTimestamp { get; init; }
    public int TotalVisibleMessageCount { get; init; }
<<<<<<< HEAD
=======
    public int UnreadMessageCount { get; init; }
    public bool HasFailedOutboundMessage { get; init; }
    public DateTimeOffset? LastReadAt { get; init; }
>>>>>>> origin/main
}

public sealed class ConversationDetailResponse : ConversationSummaryResponse
{
<<<<<<< HEAD
=======
    public new ConversationGuestSummary Guest { get; init; } = null!;
    public new ConversationReservationSummary? Reservation { get; init; }
    public new ConversationPropertySummary? Property { get; init; }
    public new ConversationAssignedUserSummary? AssignedUser { get; init; }
>>>>>>> origin/main
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
    public ConversationMessageProvider Provider { get; init; }
    public ConversationMessageDeliveryStatus? DeliveryStatus { get; init; }
    public DateTimeOffset? DeliveredAt { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
    public DateTimeOffset? FailedAt { get; init; }
    public string? SafeFailureSummary { get; init; }
    public Guid? RetryOfMessageId { get; init; }
    public int SendAttemptNumber { get; init; }
    public bool CanRetry { get; init; }
    public bool IsTemplateMessage { get; init; }
    public Guid? WhatsAppTemplateId { get; init; }
    public string? TemplateName { get; init; }
    public string? TemplateLanguageCode { get; init; }
    public string? TemplateRenderedPreview { get; init; }
    public DateTimeOffset SentAt { get; init; }
}

public sealed class ConversationHistoryResponse
{
    public Guid ConversationId { get; init; }
    public PagedResult<ConversationMessageResponse> Messages { get; init; } = null!;
<<<<<<< HEAD
=======
}

public sealed class ConversationFeedbackAnalyticsQuery
{
    public DateTimeOffset? SinceUtc { get; init; }
    public DateTimeOffset? UntilUtc { get; init; }
    public Guid? PropertyId { get; init; }
}

public sealed class ConversationFeedbackAnalyticsResponse
{
    public DateTimeOffset SinceUtc { get; init; }
    public DateTimeOffset UntilUtc { get; init; }
    public Guid? PropertyId { get; init; }
    public int TotalFeedbackCount { get; init; }
    public int HelpfulCount { get; init; }
    public int NotHelpfulCount { get; init; }
    public double HelpfulRate { get; init; }
>>>>>>> origin/main
}

public sealed class ConversationGuestSummary
{
<<<<<<< HEAD
    public Guid GuestId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
=======
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? MaskedPhoneNumber { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
}

public sealed class ConversationReservationSummary
{
    public Guid Id { get; init; }
    public string? ConfirmationNumber { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public ReservationStatus Status { get; init; }
>>>>>>> origin/main
}

public sealed class ConversationPropertySummary
{
<<<<<<< HEAD
    public Guid PropertyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
}

public sealed class ConversationReservationSummary
{
    public Guid ReservationId { get; init; }
    public string? ConfirmationNumber { get; init; }
    public DateOnly CheckInDate { get; init; }
    public DateOnly CheckOutDate { get; init; }
    public ReservationStatus Status { get; init; }
=======
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
>>>>>>> origin/main
}

public sealed class ConversationAssignedUserSummary
{
<<<<<<< HEAD
    public Guid UserId { get; init; }
=======
    public Guid Id { get; init; }
>>>>>>> origin/main
    public string FullName { get; init; } = string.Empty;
}
