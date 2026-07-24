namespace StayFlow.Api.Services.AI.Context;

public enum ConversationContextWarning
{
    MissingProperty = 0,
    MissingReservation = 1,
    NoApprovedKnowledge = 2,
    NoVisibleMessages = 3,
    ContextTruncated = 4,
    AmbiguousGuestRequest = 5,
    ConflictingKnowledge = 6
}
