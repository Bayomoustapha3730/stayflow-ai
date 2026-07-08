using StayFlow.Api.Common;

namespace StayFlow.Api.DTOs.Properties;

public sealed class PropertyQueryParameters : PaginationQuery
{
    public string? Search { get; init; }
}
