namespace StayFlow.Api.DTOs.ReservationContext;

public sealed class ReservationContextRequest
{
    public Guid? GuestId { get; init; }
    public string? Channel { get; init; }
    public string? ChannelIdentity { get; init; }
    public Guid? ConversationId { get; init; }
    public string? ExplicitReservationReference { get; init; }
    public string? ExplicitPropertyName { get; init; }
    public DateTimeOffset CurrentTimestamp { get; init; } = DateTimeOffset.UtcNow;
}
