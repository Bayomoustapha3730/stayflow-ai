using StayFlow.Api.Common;

namespace StayFlow.Api.DTOs.Properties;

public sealed class PropertyQueryParameters : PaginationQuery
{
    public Guid? CompanyId { get; init; }
    public string? Search { get; init; }
}
