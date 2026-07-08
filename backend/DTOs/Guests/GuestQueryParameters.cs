using StayFlow.Api.Common;

namespace StayFlow.Api.DTOs.Guests;

public sealed class GuestQueryParameters : PaginationQuery
{
    public string? Search { get; init; }
}
