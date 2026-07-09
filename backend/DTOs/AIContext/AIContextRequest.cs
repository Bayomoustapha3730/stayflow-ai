namespace StayFlow.Api.DTOs.AIContext;

public sealed class AIContextRequest
{
    public string GuestQuestion { get; init; } = string.Empty;
    public Guid? GuestId { get; init; }
    public Guid? ConversationId { get; init; }
    public string? Channel { get; init; }
    public string? ChannelIdentity { get; init; }
    public string? ExplicitReservationReference { get; init; }
    public string? ExplicitPropertyName { get; init; }
    public DateTimeOffset CurrentTimestamp { get; init; } = DateTimeOffset.UtcNow;
}
