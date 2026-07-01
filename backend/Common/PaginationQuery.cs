namespace StayFlow.Api.Common;

public class PaginationQuery
{
    private const int MaxPageSize = 100;

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public int NormalizedPageNumber => PageNumber < 1 ? 1 : PageNumber;
    public int NormalizedPageSize => PageSize switch
    {
        < 1 => 20,
        > MaxPageSize => MaxPageSize,
        _ => PageSize
    };
}
