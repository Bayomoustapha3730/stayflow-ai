namespace StayFlow.Api.DTOs.AIContext;

public sealed class AIConversationContext
{
    public string Channel { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool HasVerifiedReservationBinding { get; init; }
    public string Limitation { get; init; } = "Approved conversation message history and memory persistence are not implemented yet.";
}
