namespace StayFlow.Api.DTOs.AIContext;

public sealed class AIContext
{
    public AIGuestContext? Guest { get; init; }
    public AIReservationContext? Reservation { get; init; }
    public AIPropertyContext? Property { get; init; }
    public AIKnowledgeContext Knowledge { get; init; } = new();
    public AIConversationContext? Conversation { get; init; }
    public AISafetyContext Safety { get; init; } = new();
}
