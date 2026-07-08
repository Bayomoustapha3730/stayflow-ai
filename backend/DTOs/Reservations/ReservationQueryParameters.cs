using StayFlow.Api.Common;

namespace StayFlow.Api.DTOs.Reservations;

public sealed class ReservationQueryParameters : PaginationQuery
{
    public string? Search { get; init; }
    public Guid? PropertyId { get; init; }
    public Guid? PrimaryGuestId { get; init; }
    public string? Status { get; init; }
}
