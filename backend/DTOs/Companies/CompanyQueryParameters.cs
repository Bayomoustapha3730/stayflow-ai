using StayFlow.Api.Common;

namespace StayFlow.Api.DTOs.Companies;

public sealed class CompanyQueryParameters : PaginationQuery
{
    public string? Search { get; init; }
}
