namespace CqrsApiExample.Contracts.Common;

/// <summary>Which page of a list to return, read from the query string (<c>?page=2&amp;pageSize=50</c>).</summary>
/// <remarks>
///     Both values are optional. The API validates them (page 1 or more, page size between 1 and
///     100); a value that isn't a number at all is answered with <c>Request.Invalid</c>.
/// </remarks>
public record PageRequest
{
    /// <summary>The page number, starting at 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>How many items per page (1 to 100).</summary>
    public int PageSize { get; init; } = 20;
}
