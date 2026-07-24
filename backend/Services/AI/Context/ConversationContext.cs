namespace StayFlow.Api.Services.AI.Context;

public enum PropertyKnowledgeCategory
{
    WiFi = 0,
    Parking = 1,
    CheckIn = 2,
    Checkout = 3,
    HouseRules = 4,
    Amenities = 5,
    Laundry = 6,
    Thermostat = 7,
    Trash = 8,
    Emergency = 9,
    Accessibility = 10,
    FAQ = 11,
    Other = 12
}

public sealed record ConversationContextVisibleMessage(
    string MessageId,
    string SenderType,
    DateTimeOffset TimestampUtc,
    string Text);

public sealed record ConversationContextKnowledgeItem(
    string Title,
    string Content,
    PropertyKnowledgeCategory Category,
    DateTimeOffset? LastUpdated,
    int Priority,
    bool IsApproved);

public sealed record ConversationContext(
    Guid ConversationId,
    Guid TenantId,
    string Status,
    string Channel,
    string? Subject,
    bool RequiresHostAttention,
    bool HumanTakeoverEnabled,
    string? AssignedHostDisplayName,
    string GuestDisplayName,
    string? GuestEmail,
    Guid? PropertyId,
    string? PropertyName,
    Guid? ReservationId,
    string? ConfirmationNumber,
    DateOnly? CheckInDate,
    DateOnly? CheckOutDate,
    IReadOnlyCollection<ConversationContextVisibleMessage> VisibleMessages,
    IReadOnlyCollection<ConversationContextKnowledgeItem> ApprovedKnowledgeItems,
    IReadOnlyCollection<ConversationContextSource> Sources,
    IReadOnlyCollection<ConversationContextWarning> Warnings,
    bool Truncated,
    DateTimeOffset GeneratedAt);
