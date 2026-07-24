namespace StayFlow.Api.Services.AI.Context;

public enum ConversationContextSourceType
{
    Conversation = 0,
    Reservation = 1,
    Property = 2,
    PropertyKnowledge = 3
}

public sealed record ConversationContextSource(
    ConversationContextSourceType SourceType,
    string? SourceId,
    string Title,
    string? Category,
    DateTimeOffset? LastUpdated,
    string RelevanceReason,
    bool IsApproved);
