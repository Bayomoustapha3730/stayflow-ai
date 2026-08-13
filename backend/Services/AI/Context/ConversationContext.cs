using StayFlow.Api.Models;

namespace StayFlow.Api.Services.AI.Context;

public sealed record ConversationContextVisibleMessage(
    string MessageId,
    string SenderType,
    DateTimeOffset TimestampUtc,
    string Text);

public sealed record ConversationContextKnowledgeItem(
    string SourceId,
    string Title,
    string Content,
    PropertyKnowledgeCategory Category,
    DateTimeOffset? LastUpdated,
    int Priority,
    bool IsApproved,
    IReadOnlyCollection<string> Tags,
    string? Summary);

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
    string? ReservationStatus,
    IReadOnlyCollection<ConversationContextVisibleMessage> VisibleMessages,
    IReadOnlyCollection<ConversationContextKnowledgeItem> ApprovedKnowledgeItems,
    IReadOnlyCollection<ConversationContextSource> Sources,
    IReadOnlyCollection<ConversationContextWarning> Warnings,
    bool Truncated,
    DateTimeOffset GeneratedAt);
