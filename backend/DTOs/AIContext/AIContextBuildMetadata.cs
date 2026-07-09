namespace StayFlow.Api.DTOs.AIContext;

public sealed class AIContextBuildMetadata
{
    public Guid? CompanyId { get; init; }
    public Guid? GuestId { get; init; }
    public Guid? ReservationId { get; init; }
    public Guid? PropertyId { get; init; }
}
